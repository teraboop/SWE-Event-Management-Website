using EventManagement.Data;
using EventManagement.Models.Commerce;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Services;

public sealed record CheckoutResult(bool Succeeded, string Message, int? OrderId = null);

/// <summary>
/// Turns a user's cart into one completed order. There's no payment provider
/// yet, so orders complete immediately (a faux checkout). Everything happens
/// in one transaction: every line's stock is taken with a conditional UPDATE,
/// and if any line can't be filled, nothing is taken and no order is made.
/// </summary>
public class CheckoutService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly CartService _cartService;
    private readonly TicketPurchaseService _ticketPurchaseService;
    private readonly VendorProductService _productService;
    private readonly RewardService _rewardService;
    private readonly TimeProvider _timeProvider;
    private readonly EmailNotificationService _notifications;

    public CheckoutService(
        ApplicationDbContext dbContext,
        CartService cartService,
        TicketPurchaseService ticketPurchaseService,
        VendorProductService productService,
        RewardService rewardService,
        TimeProvider timeProvider,
        EmailNotificationService notifications)
    {
        _notifications = notifications;
        _rewardService = rewardService;
        _dbContext = dbContext;
        _cartService = cartService;
        _ticketPurchaseService = ticketPurchaseService;
        _productService = productService;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Places the order. <paramref name="expectedTotal"/> is the total the
    /// buyer saw on the checkout page, before any rewards discount; if prices
    /// changed since, the order is refused so nobody pays an amount they
    /// didn't agree to. <paramref name="pointsToRedeem"/> is spent for a
    /// discount (whole redemptions only); the order then earns points on
    /// what's left to pay.
    /// </summary>
    public async Task<CheckoutResult> PlaceOrderAsync(
        string userId,
        decimal expectedTotal,
        int pointsToRedeem = 0,
        CancellationToken cancellationToken = default)
    {
        if (pointsToRedeem < 0 || pointsToRedeem % RewardService.PointsPerRedemption != 0)
        {
            return Fail($"Points can only be used in blocks of {RewardService.PointsPerRedemption}.");
        }

        var cart = await _cartService.GetCartAsync(userId, cancellationToken);

        if (cart.IsEmpty)
        {
            return Fail("Your cart is empty.");
        }

        if (cart.HasProblems)
        {
            return Fail("Some items in your cart need attention before you can check out.");
        }

        if (cart.Total != expectedTotal)
        {
            return Fail("Prices in your cart have changed. Check the new total, then place your order again.");
        }

        var lines = await _dbContext.CartItems
            .Where(item => item.UserId == userId)
            .ToListAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Take stock line by line. Each UPDATE re-checks the rules at the
        // moment of writing, so a ticket or product that sold out since the
        // cart was loaded makes the whole order roll back.
        foreach (var line in lines)
        {
            var taken = line.EventTicketTypeId is int ticketTypeId
                ? await _ticketPurchaseService.TryReserveAsync(userId, ticketTypeId, line.Quantity, now, cancellationToken)
                : await _productService.TryTakeStockAsync(userId, line.VendorProductId!.Value, line.Quantity, cancellationToken);

            if (!taken)
            {
                await transaction.RollbackAsync(cancellationToken);
                var name = cart.Lines.FirstOrDefault(cartLine => cartLine.Id == line.Id)?.Name ?? "An item";
                return Fail($"{name} just sold out or stopped being available. Nothing was charged; check your cart and try again.");
            }
        }

        // Read prices inside the transaction, after taking stock, so the order
        // uses the prices in effect at the moment of purchase.
        var ticketTypeIds = lines.Where(line => line.EventTicketTypeId is not null).Select(line => line.EventTicketTypeId!.Value).ToList();
        var ticketPrices = await _dbContext.EventTicketTypes
            .Where(ticketType => ticketTypeIds.Contains(ticketType.Id))
            .ToDictionaryAsync(ticketType => ticketType.Id, ticketType => ticketType.Price, cancellationToken);

        var productIds = lines.Where(line => line.VendorProductId is not null).Select(line => line.VendorProductId!.Value).ToList();
        var products = await _dbContext.VendorProducts
            .Where(product => productIds.Contains(product.Id))
            .Select(product => new { product.Id, product.Name, product.Price, VendorName = product.VendorProfile.BusinessName })
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.Completed,
            CreatedAt = now,
            CompletedAt = now
        };

        foreach (var line in lines)
        {
            if (line.EventTicketTypeId is int ticketTypeId)
            {
                // One row per admission, each with its own check-in code.
                for (var index = 0; index < line.Quantity; index++)
                {
                    order.Tickets.Add(new Ticket
                    {
                        EventTicketTypeId = ticketTypeId,
                        UnitPrice = ticketPrices[ticketTypeId]
                    });
                }
            }
            else
            {
                var product = products[line.VendorProductId!.Value];
                order.Items.Add(new OrderItem
                {
                    VendorProductId = product.Id,
                    ProductName = product.Name,
                    VendorName = product.VendorName,
                    UnitPrice = product.Price,
                    Quantity = line.Quantity
                });
            }
        }

        order.TotalAmount =
            order.Tickets.Sum(ticket => ticket.UnitPrice) +
            order.Items.Sum(item => item.UnitPrice * item.Quantity);

        // A price edit between loading the cart and taking stock.
        if (order.TotalAmount != expectedTotal)
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            return Fail("Prices in your cart have changed. Check the new total, then place your order again.");
        }

        var discount = RewardService.DiscountFor(pointsToRedeem);
        if (discount > order.TotalAmount)
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            return Fail("That's more points than this order needs. Choose fewer points.");
        }

        order.RewardDiscount = discount;
        order.PointsRedeemed = pointsToRedeem;
        order.TotalAmount -= discount;
        order.PointsEarned = RewardService.PointsForPurchase(order.TotalAmount);

        _dbContext.Orders.Add(order);
        _dbContext.CartItems.RemoveRange(lines);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Points move inside the same transaction as the order: if the
        // balance no longer covers the redemption, the whole order is undone.
        if (pointsToRedeem > 0 &&
            !await _rewardService.TrySpendForOrderAsync(userId, pointsToRedeem, order.Id, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            return Fail("You don't have enough reward points for that discount any more. Nothing was charged.");
        }

        await _rewardService.EarnForOrderAsync(userId, order.PointsEarned, order.Id, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await _notifications.OrderPlacedAsync(order.Id, cancellationToken);
        return new CheckoutResult(true, $"Order #{order.Id} placed.", order.Id);
    }

    private static CheckoutResult Fail(string message) => new(false, message);
}

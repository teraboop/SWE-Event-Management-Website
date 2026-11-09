using EventManagement.Data;
using EventManagement.Models.Commerce;
using EventManagement.ViewModels.Cart;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Services;

public sealed record CartResult(bool Succeeded, string Message);

/// <summary>
/// The single owner of the cart rules. Adding to the cart runs the same
/// checks as buying (ticket eligibility, product availability and stock),
/// so problems show up when something is added rather than at checkout.
/// Nothing is reserved until CheckoutService places the order.
/// </summary>
public class CartService
{
    /// <summary>Most of one product a single cart line can hold.</summary>
    public const int MaxProductQuantity = 10;

    /// <summary>Most separate lines a cart can hold.</summary>
    public const int MaxLines = 20;

    private readonly ApplicationDbContext _dbContext;
    private readonly TicketPurchaseService _ticketPurchaseService;
    private readonly VendorProductService _productService;

    public CartService(
        ApplicationDbContext dbContext,
        TicketPurchaseService ticketPurchaseService,
        VendorProductService productService)
    {
        _dbContext = dbContext;
        _ticketPurchaseService = ticketPurchaseService;
        _productService = productService;
    }

    /// <summary>Total number of tickets and items in the cart, for the navbar badge.</summary>
    public async Task<int> CountAsync(string userId, CancellationToken cancellationToken = default) =>
        await _dbContext.CartItems
            .Where(item => item.UserId == userId)
            .SumAsync(item => (int?)item.Quantity, cancellationToken) ?? 0;

    /// <summary>
    /// Adds tickets to the cart, or more of them to an existing line. The
    /// combined quantity must pass the same eligibility check as a purchase
    /// and stay within <see cref="TicketPurchaseService.MaxTicketsPerOrder"/>.
    /// </summary>
    public async Task<CartResult> AddTicketsAsync(
        string userId,
        int eventId,
        int ticketTypeId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity < 1)
        {
            return Fail("Choose at least one ticket.");
        }

        var line = await _dbContext.CartItems.SingleOrDefaultAsync(
            item => item.UserId == userId && item.EventTicketTypeId == ticketTypeId,
            cancellationToken);

        var newQuantity = (line?.Quantity ?? 0) + quantity;
        if (newQuantity > TicketPurchaseService.MaxTicketsPerOrder)
        {
            return Fail($"You can have up to {TicketPurchaseService.MaxTicketsPerOrder} of each ticket type in your cart.");
        }

        var eligibility = await _ticketPurchaseService.CheckEligibilityAsync(
            userId, eventId, ticketTypeId, newQuantity, cancellationToken);
        if (!eligibility.IsEligible)
        {
            return Fail(eligibility.Message);
        }

        return await SaveLineAsync(userId, line, newQuantity, ticketTypeId: ticketTypeId, productId: null, cancellationToken);
    }

    /// <summary>
    /// Adds a shop product to the cart, or more of it to an existing line. The
    /// product must be in the shop, not the buyer's own, and in stock.
    /// </summary>
    public async Task<CartResult> AddProductAsync(
        string userId,
        int productId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (quantity < 1)
        {
            return Fail("Choose a quantity of at least one.");
        }

        var line = await _dbContext.CartItems.SingleOrDefaultAsync(
            item => item.UserId == userId && item.VendorProductId == productId,
            cancellationToken);

        var newQuantity = (line?.Quantity ?? 0) + quantity;
        var problem = await CheckProductAsync(userId, productId, newQuantity, cancellationToken);
        if (problem is not null)
        {
            return Fail(problem);
        }

        return await SaveLineAsync(userId, line, newQuantity, ticketTypeId: null, productId: productId, cancellationToken);
    }

    /// <summary>Changes a line's quantity; zero or less removes it.</summary>
    public async Task<CartResult> UpdateQuantityAsync(
        string userId,
        int lineId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var line = await _dbContext.CartItems
            .Include(item => item.EventTicketType)
            .SingleOrDefaultAsync(item => item.Id == lineId && item.UserId == userId, cancellationToken);

        if (line is null)
        {
            return Fail("That item is no longer in your cart.");
        }

        if (quantity <= 0)
        {
            _dbContext.CartItems.Remove(line);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new CartResult(true, "Removed from your cart.");
        }

        if (line.EventTicketType is { } ticketType)
        {
            if (quantity > TicketPurchaseService.MaxTicketsPerOrder)
            {
                return Fail($"You can have up to {TicketPurchaseService.MaxTicketsPerOrder} of each ticket type in your cart.");
            }

            var eligibility = await _ticketPurchaseService.CheckEligibilityAsync(
                userId, ticketType.EventId, ticketType.Id, quantity, cancellationToken);
            if (!eligibility.IsEligible)
            {
                return Fail(eligibility.Message);
            }
        }
        else
        {
            var problem = await CheckProductAsync(userId, line.VendorProductId!.Value, quantity, cancellationToken);
            if (problem is not null)
            {
                return Fail(problem);
            }
        }

        line.Quantity = quantity;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new CartResult(true, "Cart updated.");
    }

    public async Task<CartResult> RemoveAsync(string userId, int lineId, CancellationToken cancellationToken = default)
    {
        var removed = await _dbContext.CartItems
            .Where(item => item.Id == lineId && item.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        return removed == 1
            ? new CartResult(true, "Removed from your cart.")
            : Fail("That item is no longer in your cart.");
    }

    /// <summary>
    /// The cart with current prices, oldest line first. Each line is checked
    /// again (sold out, sales ended, product removed...) so the cart can show
    /// what needs fixing before checkout.
    /// </summary>
    public async Task<CartViewModel> GetCartAsync(string userId, CancellationToken cancellationToken = default)
    {
        var ticketRows = await _dbContext.CartItems
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.EventTicketTypeId != null)
            .Select(item => new
            {
                item.Id,
                item.Quantity,
                item.AddedAt,
                TicketTypeId = item.EventTicketType!.Id,
                item.EventTicketType.Name,
                item.EventTicketType.Price,
                Remaining = item.EventTicketType.QuantityAvailable - item.EventTicketType.QuantitySold,
                item.EventTicketType.EventId,
                EventTitle = item.EventTicketType.Event.Title,
                EventStartsAt = item.EventTicketType.Event.StartsAt,
                item.EventTicketType.Event.BackgroundImageId
            })
            .ToListAsync(cancellationToken);

        var lines = new List<(DateTimeOffset AddedAt, CartLineViewModel Line)>();

        foreach (var row in ticketRows)
        {
            var eligibility = await _ticketPurchaseService.CheckEligibilityAsync(
                userId, row.EventId, row.TicketTypeId, row.Quantity, cancellationToken);

            lines.Add((row.AddedAt, new CartLineViewModel
            {
                Id = row.Id,
                IsTicket = true,
                Name = $"{row.Name} ticket",
                Context = row.EventTitle,
                EventStartsAt = row.EventStartsAt,
                LinkId = row.EventId,
                UnitPrice = row.Price,
                Quantity = row.Quantity,
                MaxQuantity = Math.Max(1, Math.Min(TicketPurchaseService.MaxTicketsPerOrder, row.Remaining)),
                Problem = eligibility.IsEligible ? null : eligibility.Message
            }));
        }

        var available = _productService.AvailableProducts();
        var productRows = await _dbContext.CartItems
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.VendorProductId != null)
            .Select(item => new
            {
                item.Id,
                item.Quantity,
                item.AddedAt,
                ProductId = item.VendorProduct!.Id,
                item.VendorProduct.Name,
                item.VendorProduct.Price,
                item.VendorProduct.QuantityInStock,
                item.VendorProduct.ImageId,
                VendorName = item.VendorProduct.VendorProfile.BusinessName,
                IsOwn = item.VendorProduct.VendorProfile.OwnerUserId == userId,
                IsAvailable = available.Any(product => product.Id == item.VendorProductId)
            })
            .ToListAsync(cancellationToken);

        foreach (var row in productRows)
        {
            lines.Add((row.AddedAt, new CartLineViewModel
            {
                Id = row.Id,
                IsTicket = false,
                Name = row.Name,
                Context = row.VendorName,
                LinkId = row.ProductId,
                ImageId = row.ImageId,
                UnitPrice = row.Price,
                Quantity = row.Quantity,
                MaxQuantity = Math.Max(1, Math.Min(MaxProductQuantity, row.QuantityInStock)),
                Problem = DescribeProductProblem(row.IsAvailable, row.IsOwn, row.QuantityInStock, row.Quantity)
            }));
        }

        return new CartViewModel
        {
            Lines = lines.OrderBy(entry => entry.AddedAt).Select(entry => entry.Line).ToList()
        };
    }

    private async Task<string?> CheckProductAsync(string userId, int productId, int quantity, CancellationToken cancellationToken)
    {
        if (quantity > MaxProductQuantity)
        {
            return $"You can have up to {MaxProductQuantity} of each product in your cart.";
        }

        var product = await _productService.AvailableProducts()
            .Where(item => item.Id == productId)
            .Select(item => new { item.QuantityInStock, item.VendorProfile.OwnerUserId })
            .SingleOrDefaultAsync(cancellationToken);

        return product is null
            ? DescribeProductProblem(isAvailable: false, isOwn: false, 0, quantity)
            : DescribeProductProblem(isAvailable: true, product.OwnerUserId == userId, product.QuantityInStock, quantity);
    }

    private static string? DescribeProductProblem(bool isAvailable, bool isOwn, int inStock, int quantity)
    {
        if (!isAvailable)
        {
            return "This product is no longer for sale.";
        }

        if (isOwn)
        {
            return "You can't buy your own product.";
        }

        if (inStock <= 0)
        {
            return "This product is sold out.";
        }

        if (inStock < quantity)
        {
            return $"Only {inStock} left. Lower the quantity to continue.";
        }

        return null;
    }

    private async Task<CartResult> SaveLineAsync(
        string userId,
        CartItem? line,
        int newQuantity,
        int? ticketTypeId,
        int? productId,
        CancellationToken cancellationToken)
    {
        if (line is not null)
        {
            line.Quantity = newQuantity;
        }
        else
        {
            var lineCount = await _dbContext.CartItems.CountAsync(item => item.UserId == userId, cancellationToken);
            if (lineCount >= MaxLines)
            {
                return Fail($"Your cart can hold up to {MaxLines} different items. Check out or remove something first.");
            }

            line = new CartItem
            {
                UserId = userId,
                EventTicketTypeId = ticketTypeId,
                VendorProductId = productId,
                Quantity = newQuantity
            };
            _dbContext.CartItems.Add(line);
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The one-line-per-item index caught the same item being added
            // twice at once (e.g. a double click).
            _dbContext.Entry(line).State = EntityState.Detached;
            return Fail("Your cart was just updated. Check it and try again.");
        }

        return new CartResult(true, newQuantity == 1 ? "Added to your cart." : $"Your cart now has {newQuantity} of these.");
    }

    private static CartResult Fail(string message) => new(false, message);
}

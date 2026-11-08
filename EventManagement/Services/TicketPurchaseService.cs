using EventManagement.Data;
using EventManagement.Models.Commerce;
using EventManagement.Models.Events;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Services;

/// <summary>Why a ticket purchase is not allowed. Callers can branch on the code.</summary>
public enum TicketIneligibilityReason
{
    NotSignedIn,
    TicketTypeNotFound,
    EventNotPublished,
    EventAlreadyStarted,
    NotSoldOnPlatform,
    OrganizerOwnEvent,
    TicketTypeDisabled,
    SalesNotStarted,
    SalesEnded,
    InvalidQuantity,
    NotEnoughRemaining
}

public sealed record TicketEligibilityResult(
    TicketIneligibilityReason? Reason,
    string Message)
{
    public bool IsEligible => Reason is null;

    public static readonly TicketEligibilityResult Eligible = new(null, string.Empty);
}

public sealed record TicketPurchaseResult(
    bool Succeeded,
    string Message,
    int? OrderId = null);

/// <summary>
/// The only place that decides whether someone may buy tickets, and the only
/// place that records a purchase. Pages ask this service instead of
/// re-implementing the rules, so the Details page, the purchase endpoint and
/// future features (cart, reviews, rewards) all agree on what "eligible" and
/// "verified purchaser" mean.
/// </summary>
public class TicketPurchaseService
{
    public const int MaxTicketsPerOrder = 10;

    private readonly ApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public TicketPurchaseService(ApplicationDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// A user is eligible to buy <paramref name="quantity"/> tickets of a
    /// ticket type when all of these hold:
    /// <list type="number">
    /// <item>they are signed in;</item>
    /// <item>the ticket type exists and belongs to <paramref name="eventId"/>;</item>
    /// <item>the event is Published and has not started yet;</item>
    /// <item>the event sells tickets on this platform;</item>
    /// <item>they are not the event's organizer (so organizers can't
    /// self-verify for reviews or earn rewards on their own event);</item>
    /// <item>the ticket type is enabled and inside its sales window;</item>
    /// <item>1 &lt;= quantity &lt;= <see cref="MaxTicketsPerOrder"/>;</item>
    /// <item>at least that many tickets remain.</item>
    /// </list>
    /// </summary>
    public async Task<TicketEligibilityResult> CheckEligibilityAsync(
        string? userId,
        int eventId,
        int ticketTypeId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return Ineligible(TicketIneligibilityReason.NotSignedIn, "Log in to buy tickets.");
        }

        var snapshot = await _dbContext.EventTicketTypes
            .AsNoTracking()
            .Where(ticketType => ticketType.Id == ticketTypeId && ticketType.EventId == eventId)
            .Select(ticketType => new TicketTypeSnapshot(
                ticketType.Name,
                ticketType.IsEnabled,
                ticketType.SalesStartAt,
                ticketType.SalesEndAt,
                ticketType.QuantityAvailable - ticketType.QuantitySold,
                ticketType.Event.Status,
                ticketType.Event.StartsAt,
                ticketType.Event.TicketSalesMode,
                ticketType.Event.OrganizerId))
            .SingleOrDefaultAsync(cancellationToken);

        return Evaluate(snapshot, userId, quantity, _timeProvider.GetUtcNow());
    }

    /// <summary>
    /// Re-checks eligibility, then reserves the tickets and records a
    /// completed order in a single transaction. The reservation is a
    /// conditional UPDATE that only succeeds while enough tickets remain, so
    /// two buyers racing for the last ticket cannot both get it.
    /// </summary>
    public async Task<TicketPurchaseResult> PurchaseAsync(
        string userId,
        int eventId,
        int ticketTypeId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var eligibility = await CheckEligibilityAsync(
            userId, eventId, ticketTypeId, quantity, cancellationToken);

        if (!eligibility.IsEligible)
        {
            return new TicketPurchaseResult(false, eligibility.Message);
        }

        var now = _timeProvider.GetUtcNow();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Atomic reserve: the WHERE clause re-checks capacity, the enabled
        // flag and the sales window at the moment of writing. If anything
        // changed since the eligibility check, zero rows are updated.
        var reserved = await _dbContext.EventTicketTypes
            .Where(ticketType =>
                ticketType.Id == ticketTypeId &&
                ticketType.EventId == eventId &&
                ticketType.IsEnabled &&
                ticketType.SalesStartAt <= now &&
                ticketType.SalesEndAt > now &&
                ticketType.QuantitySold + quantity <= ticketType.QuantityAvailable)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    ticketType => ticketType.QuantitySold,
                    ticketType => ticketType.QuantitySold + quantity),
                cancellationToken);

        if (reserved == 0)
        {
            await transaction.RollbackAsync(cancellationToken);

            // Explain using the current state rather than a generic failure.
            var recheck = await CheckEligibilityAsync(
                userId, eventId, ticketTypeId, quantity, cancellationToken);

            return new TicketPurchaseResult(
                false,
                recheck.IsEligible
                    ? "Those tickets were just taken by another buyer. Please try again."
                    : recheck.Message);
        }

        // Read the price inside the transaction, after reserving, so the
        // buyer pays the price in effect at the moment of purchase.
        var unitPrice = await _dbContext.EventTicketTypes
            .Where(ticketType => ticketType.Id == ticketTypeId)
            .Select(ticketType => ticketType.Price)
            .SingleAsync(cancellationToken);

        var order = new Order
        {
            UserId = userId,
            Status = OrderStatus.Completed,
            TotalAmount = unitPrice * quantity,
            CreatedAt = now,
            CompletedAt = now
        };

        for (var index = 0; index < quantity; index++)
        {
            order.Tickets.Add(new Ticket
            {
                EventTicketTypeId = ticketTypeId,
                UnitPrice = unitPrice
            });
        }

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new TicketPurchaseResult(true, string.Empty, order.Id);
    }

    /// <summary>
    /// The "verified purchaser" check: true when the user holds at least one
    /// ticket from a completed order for the event. Event reviews (#13) and
    /// the rewards program (#5) should use this rather than saved events.
    /// </summary>
    public Task<bool> HasVerifiedPurchaseAsync(
        string userId,
        int eventId,
        CancellationToken cancellationToken = default) =>
        CompletedTicketsFor(userId, eventId).AnyAsync(cancellationToken);

    /// <summary>Number of tickets the user holds for an event from completed orders.</summary>
    public Task<int> CountTicketsHeldAsync(
        string userId,
        int eventId,
        CancellationToken cancellationToken = default) =>
        CompletedTicketsFor(userId, eventId).CountAsync(cancellationToken);

    private IQueryable<Ticket> CompletedTicketsFor(string userId, int eventId) =>
        _dbContext.Tickets.Where(ticket =>
            ticket.Order.UserId == userId &&
            ticket.Order.Status == OrderStatus.Completed &&
            ticket.EventTicketType.EventId == eventId);

    /// <summary>
    /// The rules as a pure function of the data, so they're easy to read and
    /// unit test without a database. Rules run in the order a buyer would
    /// want them explained: event-level problems before ticket-level ones.
    /// </summary>
    private static TicketEligibilityResult Evaluate(
        TicketTypeSnapshot? ticketType,
        string userId,
        int quantity,
        DateTimeOffset now)
    {
        if (ticketType is null)
        {
            return Ineligible(TicketIneligibilityReason.TicketTypeNotFound, "That ticket type is not available for this event.");
        }

        if (ticketType.EventStatus != EventStatus.Published)
        {
            return Ineligible(TicketIneligibilityReason.EventNotPublished, "Tickets are not on sale for this event.");
        }

        if (ticketType.EventStartsAt <= now)
        {
            return Ineligible(TicketIneligibilityReason.EventAlreadyStarted, "This event has already started, so tickets are no longer on sale.");
        }

        if (ticketType.SalesMode != TicketSalesMode.OnPlatform)
        {
            return Ineligible(TicketIneligibilityReason.NotSoldOnPlatform, "Tickets for this event are not sold on this site.");
        }

        if (ticketType.OrganizerId == userId)
        {
            return Ineligible(TicketIneligibilityReason.OrganizerOwnEvent, "Organizers can't buy tickets to their own event.");
        }

        if (!ticketType.IsEnabled)
        {
            return Ineligible(TicketIneligibilityReason.TicketTypeDisabled, $"{ticketType.Name} tickets are not currently offered.");
        }

        if (ticketType.SalesStartAt > now)
        {
            return Ineligible(TicketIneligibilityReason.SalesNotStarted, $"Sales for {ticketType.Name} tickets have not started yet.");
        }

        if (ticketType.SalesEndAt <= now)
        {
            return Ineligible(TicketIneligibilityReason.SalesEnded, $"Sales for {ticketType.Name} tickets have ended.");
        }

        if (quantity < 1 || quantity > MaxTicketsPerOrder)
        {
            return Ineligible(TicketIneligibilityReason.InvalidQuantity, $"Choose between 1 and {MaxTicketsPerOrder} tickets.");
        }

        if (ticketType.Remaining < quantity)
        {
            return Ineligible(
                TicketIneligibilityReason.NotEnoughRemaining,
                ticketType.Remaining <= 0
                    ? $"{ticketType.Name} tickets are sold out."
                    : $"Only {ticketType.Remaining} {ticketType.Name} tickets are left.");
        }

        return TicketEligibilityResult.Eligible;
    }

    private static TicketEligibilityResult Ineligible(TicketIneligibilityReason reason, string message) =>
        new(reason, message);

    private sealed record TicketTypeSnapshot(
        string Name,
        bool IsEnabled,
        DateTimeOffset SalesStartAt,
        DateTimeOffset SalesEndAt,
        int Remaining,
        EventStatus EventStatus,
        DateTimeOffset EventStartsAt,
        TicketSalesMode SalesMode,
        string OrganizerId);
}

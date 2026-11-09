using EventManagement.Data;

namespace EventManagement.Models.Commerce;

/// <summary>
/// Order lifecycle. Purchases complete immediately for now because the site
/// has no payment provider yet. Pending is reserved for a future payment
/// step, and Cancelled/Refunded for a future cancellation flow (which must
/// also give the tickets back to EventTicketType.QuantitySold).
/// </summary>
public enum OrderStatus
{
    Pending = 0,
    Completed = 1,
    Cancelled = 2,
    Refunded = 3
}

/// <summary>
/// A purchase made by one user. The order is not tied to an event or to
/// tickets specifically: tickets hang off it as line items, so merchandise
/// line items and a shared cart can be added later without reshaping it.
/// </summary>
public class Order
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public decimal TotalAmount { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    public ICollection<Ticket> Tickets { get; set; } = [];

    /// <summary>Merchandise lines, one per product.</summary>
    public ICollection<OrderItem> Items { get; set; } = [];
}

/// <summary>
/// One admission. Each ticket gets its own row (not one row with a quantity)
/// so each can carry a unique code for check-in. The price is copied at
/// purchase time so later price edits don't rewrite purchase history.
/// </summary>
public class Ticket
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public int EventTicketTypeId { get; set; }

    public Events.EventTicketType EventTicketType { get; set; } = null!;

    public decimal UnitPrice { get; set; }

    public Guid Code { get; set; } = Guid.NewGuid();
}

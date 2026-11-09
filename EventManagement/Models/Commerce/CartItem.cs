using EventManagement.Data;
using EventManagement.Models.Events;
using EventManagement.Models.Vendors;

namespace EventManagement.Models.Commerce;

/// <summary>
/// One line in a user's cart: either tickets of one ticket type or one shop
/// product, never both (a check constraint enforces exactly one). Adding the
/// same thing again raises the quantity instead of adding a second line.
/// Nothing is reserved while it sits in the cart; stock is only taken at
/// checkout, so the cart shows a problem if something sells out meanwhile.
/// </summary>
public class CartItem
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public int? EventTicketTypeId { get; set; }

    public EventTicketType? EventTicketType { get; set; }

    public int? VendorProductId { get; set; }

    public VendorProduct? VendorProduct { get; set; }

    public int Quantity { get; set; }

    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool IsTicket => EventTicketTypeId is not null;
}

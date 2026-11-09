using EventManagement.Models.Vendors;

namespace EventManagement.Models.Commerce;

/// <summary>
/// A merchandise line on an order. Tickets get one row each (see Ticket) so
/// each can carry a check-in code; merchandise is one row per product with
/// a quantity. The name, vendor and price are copied at purchase time so
/// later edits to the product don't rewrite purchase history.
/// </summary>
public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public int VendorProductId { get; set; }

    public VendorProduct VendorProduct { get; set; } = null!;

    public required string ProductName { get; set; }

    public required string VendorName { get; set; }

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }
}

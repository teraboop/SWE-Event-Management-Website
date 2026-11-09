using EventManagement.Models.Events;

namespace EventManagement.Models.Vendors;

/// <summary>
/// One item a vendor sells through the website's marketplace. Only vendors
/// approved for online sales can list products, and VendorProductService
/// decides what shoppers can see.
/// <para>
/// Built with a future cart in mind:
/// <list type="bullet">
/// <item>products are archived, never deleted, so order lines can keep
/// pointing at them;</item>
/// <item>stock is a single count with a check constraint, so checkout can take
/// stock with one conditional UPDATE (<c>WHERE QuantityInStock &gt;= @qty</c>),
/// the way ticket purchases use <c>EventTicketType.QuantitySold</c>;</item>
/// <item>order lines should copy the name and price at purchase time, like
/// <c>Ticket.UnitPrice</c>, so later edits don't rewrite purchase history.</item>
/// </list>
/// </para>
/// </summary>
public class VendorProduct
{
    public int Id { get; set; }

    public int VendorProfileId { get; set; }

    public VendorProfile VendorProfile { get; set; } = null!;

    public required string Name { get; set; }

    public required string Description { get; set; }

    public decimal Price { get; set; }

    /// <summary>How many are left to sell. Zero shows as "Out of stock".</summary>
    public int QuantityInStock { get; set; }

    /// <summary>The vendor's on/off switch. Hidden products stay in the vendor's list but not in the shop.</summary>
    public bool IsListed { get; set; } = true;

    /// <summary>Optional product photo, stored in the shared image table.</summary>
    public int? ImageId { get; set; }

    public EventImage? Image { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Set when the vendor (or an admin) removes the product. Archived products
    /// disappear from the shop and the vendor's list but stay in the database.
    /// </summary>
    public DateTimeOffset? ArchivedAt { get; set; }
}

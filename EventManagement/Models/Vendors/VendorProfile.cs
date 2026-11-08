using EventManagement.Data;

namespace EventManagement.Models.Vendors;

/// <summary>
/// A user's vendor identity on the site; each user has at most one. Both
/// kinds of vendor application are submitted from this profile, so a vendor
/// enters their business details once and reuses them for every application.
/// </summary>
public class VendorProfile
{
    public int Id { get; set; }

    public string OwnerUserId { get; set; } = string.Empty;

    public ApplicationUser Owner { get; set; } = null!;

    public required string BusinessName { get; set; }

    /// <summary>What the vendor sells, e.g. "Handmade candles and soaps".</summary>
    public required string ProductsOffered { get; set; }

    /// <summary>A description of the business as a whole.</summary>
    public required string Description { get; set; }

    public required string ContactEmail { get; set; }

    public string? WebsiteUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<VendorApplication> Applications { get; set; } = [];
}

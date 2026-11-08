using EventManagement.Models.Applications;
using EventManagement.Models.Events;

namespace EventManagement.Models.Vendors;

/// <summary>What a vendor is asking for permission to do.</summary>
public enum VendorApplicationKind
{
    /// <summary>Sell merchandise through the website. Reviewed by an admin.</summary>
    OnlineSales = 0,

    /// <summary>Sell at one specific event. Reviewed by that event's organizer or an admin.</summary>
    Event = 1
}

/// <summary>
/// One request from a vendor profile. The two kinds are independent: a
/// vendor doesn't need online approval to apply to an event, or the reverse.
/// </summary>
public class VendorApplication
{
    public int Id { get; set; }

    public int VendorProfileId { get; set; }

    public VendorProfile VendorProfile { get; set; } = null!;

    public VendorApplicationKind Kind { get; set; }

    /// <summary>Set only for <see cref="VendorApplicationKind.Event"/> applications.</summary>
    public int? EventId { get; set; }

    public Event? Event { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;

    // Copies of the profile at submission time. If the vendor edits their
    // profile while an application is pending, the reviewer still sees what
    // was submitted, and decided applications show what was approved.
    public required string BusinessName { get; set; }

    public required string ProductsOffered { get; set; }

    public required string Description { get; set; }

    public required string ContactEmail { get; set; }

    public string? WebsiteUrl { get; set; }

    /// <summary>Optional note from the vendor to the reviewer.</summary>
    public string? Message { get; set; }

    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When the application left Submitted (approved, rejected or withdrawn).</summary>
    public DateTimeOffset? DecidedAt { get; set; }

    /// <summary>
    /// Who approved or rejected it. Stored as a plain ID and name, with no
    /// foreign key, so deleting the reviewer's account doesn't erase or block
    /// the record of the decision.
    /// </summary>
    public string? ReviewedByUserId { get; set; }

    public string? ReviewedByName { get; set; }

    /// <summary>Reviewer's note to the vendor. Required when rejecting.</summary>
    public string? ReviewNote { get; set; }
}

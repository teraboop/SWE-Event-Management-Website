using EventManagement.Models.Applications;

namespace EventManagement.ViewModels.Vendors;

/// <summary>
/// One application as a reviewer sees it. The same card renders on the
/// organizer's per-event page and the admin's online-sales queue.
/// </summary>
public class VendorApplicationReviewItem
{
    public int Id { get; set; }

    public string ApplicantUserName { get; set; } = string.Empty;

    public string BusinessName { get; set; } = string.Empty;

    public string ProductsOffered { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ContactEmail { get; set; } = string.Empty;

    public string? WebsiteUrl { get; set; }

    public string? Message { get; set; }

    public ApplicationStatus Status { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }

    public string? ReviewedByName { get; set; }

    public string? ReviewNote { get; set; }

    /// <summary>URL the approve/reject form posts to; differs between the organizer and admin pages.</summary>
    public string ReviewFormAction { get; set; } = string.Empty;
}

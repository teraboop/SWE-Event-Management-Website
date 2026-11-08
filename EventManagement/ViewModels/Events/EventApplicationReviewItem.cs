using EventManagement.Models.Applications;

namespace EventManagement.ViewModels.Events;

/// <summary>One event application as an admin reviews it.</summary>
public class EventApplicationReviewItem
{
    public int Id { get; set; }

    public string ApplicantUserName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string EventCategoryName { get; set; } = string.Empty;

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public string VenueName { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string? PostalCode { get; set; }

    public string? ExternalWebsiteUrl { get; set; }

    public int? BackgroundImageId { get; set; }

    public IReadOnlyList<string> Interests { get; set; } = [];

    public string ContactName { get; set; } = string.Empty;

    public string ContactEmail { get; set; } = string.Empty;

    public string? ContactPhone { get; set; }

    public string? OrganizationName { get; set; }

    public string? Message { get; set; }

    public ApplicationStatus Status { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }

    public string? ReviewedByName { get; set; }

    public string? ReviewNote { get; set; }

    public int? CreatedEventId { get; set; }

    /// <summary>URL the approve/reject form posts to.</summary>
    public string ReviewFormAction { get; set; } = string.Empty;
}

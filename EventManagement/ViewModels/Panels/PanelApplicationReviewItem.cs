using EventManagement.Models.Applications;

namespace EventManagement.ViewModels.Panels;

/// <summary>One panel proposal as the organizer (or an admin) reviews it.</summary>
public class PanelApplicationReviewItem
{
    public int Id { get; set; }

    public string ApplicantUserName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Panelists { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }

    public string ContactEmail { get; set; } = string.Empty;

    public string? Message { get; set; }

    public ApplicationStatus Status { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }

    public string? ReviewedByName { get; set; }

    public string? ReviewNote { get; set; }

    /// <summary>URL the approve/reject form posts to.</summary>
    public string ReviewFormAction { get; set; } = string.Empty;
}

/// <summary>An approved panel as the public sees it on the event page. No contact details.</summary>
public class EventPanelViewModel
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Panelists { get; set; } = string.Empty;

    public int DurationMinutes { get; set; }
}

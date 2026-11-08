using EventManagement.Data;
using EventManagement.Models.Applications;
using EventManagement.Models.Events;

namespace EventManagement.Models.Panels;

/// <summary>
/// A user's proposal to run a panel at one event, reviewed by that event's
/// organizer (or an admin). Unlike vendor applications there is no profile:
/// each proposal stands on its own, and one user may propose several
/// different panels for the same event. Approved panels are listed publicly
/// on the event page.
/// </summary>
public class PanelApplication
{
    public int Id { get; set; }

    public int EventId { get; set; }

    public Event Event { get; set; } = null!;

    public string ApplicantUserId { get; set; } = string.Empty;

    public ApplicationUser Applicant { get; set; } = null!;

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;

    public required string Title { get; set; }

    /// <summary>What the panel is about; shown publicly once approved.</summary>
    public required string Description { get; set; }

    /// <summary>Who will be on the panel, one per line (name and a short bio).</summary>
    public required string Panelists { get; set; }

    public int DurationMinutes { get; set; }

    /// <summary>How the organizer can reach the applicant. Never shown publicly.</summary>
    public required string ContactEmail { get; set; }

    /// <summary>Optional note to the organizer, e.g. equipment or scheduling needs.</summary>
    public string? Message { get; set; }

    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>When the proposal left Submitted (approved, rejected or withdrawn).</summary>
    public DateTimeOffset? DecidedAt { get; set; }

    /// <summary>
    /// Who approved or rejected it, stored without a foreign key so deleting
    /// the reviewer's account keeps the record of the decision.
    /// </summary>
    public string? ReviewedByUserId { get; set; }

    public string? ReviewedByName { get; set; }

    /// <summary>Reviewer's note to the applicant. Required when rejecting.</summary>
    public string? ReviewNote { get; set; }
}

using EventManagement.Data;
using EventManagement.Models.Applications;
using EventManagement.Models.Profile;

namespace EventManagement.Models.Events;

/// <summary>
/// A user's request to list a new event on the website, reviewed by an admin.
/// It carries everything needed to build the listing plus the organizer's
/// contact details. Approving it creates a published Event owned by the
/// applicant and gives them the Organizer role.
/// </summary>
public class EventApplication
{
    public int Id { get; set; }

    public string ApplicantUserId { get; set; } = string.Empty;

    public ApplicationUser Applicant { get; set; } = null!;

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;

    // The proposed listing. Copied onto the new Event when approved; the
    // application keeps what was submitted even if the organizer later edits
    // the event.
    public required string Title { get; set; }

    public required string Description { get; set; }

    public int EventCategoryId { get; set; }

    public EventCategory EventCategory { get; set; } = null!;

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public required string VenueName { get; set; }

    public required string City { get; set; }

    public string? PostalCode { get; set; }

    public string? ExternalWebsiteUrl { get; set; }

    public int? BackgroundImageId { get; set; }

    public EventImage? BackgroundImage { get; set; }

    public ICollection<EventApplicationInterest> Interests { get; set; } = [];

    // How the admin can reach the organizer. Never shown publicly.
    public required string ContactName { get; set; }

    public required string ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    /// <summary>The company or group running the event, if any.</summary>
    public string? OrganizationName { get; set; }

    /// <summary>Optional note to the reviewing admin.</summary>
    public string? Message { get; set; }

    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? DecidedAt { get; set; }

    /// <summary>Stored without a foreign key so deleting the reviewer's account keeps the record.</summary>
    public string? ReviewedByUserId { get; set; }

    public string? ReviewedByName { get; set; }

    /// <summary>Admin's note to the applicant. Required when rejecting.</summary>
    public string? ReviewNote { get; set; }

    /// <summary>The event created when this application was approved.</summary>
    public int? CreatedEventId { get; set; }

    public Event? CreatedEvent { get; set; }
}

public class EventApplicationInterest
{
    public int EventApplicationId { get; set; }

    public EventApplication EventApplication { get; set; } = null!;

    public int InterestCategoryId { get; set; }

    public InterestCategory InterestCategory { get; set; } = null!;
}

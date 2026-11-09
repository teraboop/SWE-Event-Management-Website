namespace EventManagement.Models.Events;

/// <summary>
/// An update the organizer posts on their event's page, such as a schedule
/// change or a reminder. Shown publicly on published events, pinned ones
/// first. EventAnnouncementService owns who may post and edit.
/// </summary>
public class EventAnnouncement
{
    public int Id { get; set; }

    public int EventId { get; set; }

    public Event Event { get; set; } = null!;

    public required string Title { get; set; }

    public required string Body { get; set; }

    /// <summary>Pinned announcements stay at the top of the list, above newer ones.</summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// Who posted it, stored without a foreign key (like application
    /// reviewers) so deleting that account keeps the announcement.
    /// </summary>
    public string PostedByUserId { get; set; } = string.Empty;

    public string PostedByName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Set when the announcement is edited, so readers can see it changed.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}

using EventManagement.Data;
using EventManagement.Models.Events;

namespace EventManagement.Models.Reviews;

/// <summary>
/// One attendee's star rating, and optional written review, of an event they
/// held a ticket for. Each user gets one review per event, which they can
/// edit or delete; admins can remove any review. EventReviewService owns the
/// rules for who may write one.
/// </summary>
public class EventReview
{
    public int Id { get; set; }

    public int EventId { get; set; }

    public Event Event { get; set; } = null!;

    public string AuthorUserId { get; set; } = string.Empty;

    public ApplicationUser Author { get; set; } = null!;

    /// <summary>1 to 5 stars, enforced by a check constraint.</summary>
    public int Rating { get; set; }

    /// <summary>Optional written review, shown publicly on the event page.</summary>
    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Set when the author edits the review, so readers can see it changed.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}

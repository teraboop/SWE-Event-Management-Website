using System.ComponentModel.DataAnnotations;

namespace EventManagement.ViewModels.Events;

/// <summary>The organizer's post/edit announcement form.</summary>
public class EventAnnouncementInputModel
{
    [Required(ErrorMessage = "Give the announcement a headline.")]
    [StringLength(150)]
    [Display(Name = "Headline")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Write the announcement.")]
    [StringLength(2000)]
    [Display(Name = "Message")]
    public string Body { get; set; } = string.Empty;

    [Display(Name = "Pin to the top of the event page")]
    public bool IsPinned { get; set; }
}

public class EventAnnouncementItem
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public bool IsPinned { get; set; }

    public string PostedByName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Posted recently enough to get a "New" badge.</summary>
    public bool IsNew { get; set; }
}

/// <summary>An event's most recent announcement, for the My tickets page.</summary>
public record LatestAnnouncement(string Title, DateTimeOffset CreatedAt, bool IsNew);

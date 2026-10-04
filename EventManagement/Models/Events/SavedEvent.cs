using EventManagement.Data;

namespace EventManagement.Models.Events;

public class SavedEvent
{
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public int EventId { get; set; }

    public Event Event { get; set; } = null!;

    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
}
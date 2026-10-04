using EventManagement.Data;

namespace EventManagement.Models.Events;

public class Event
{
    public int Id { get; set; }

    public string OrganizerId { get; set; } = string.Empty;

    public ApplicationUser Organizer { get; set; } = null!;

    public int EventCategoryId { get; set; }

    public EventCategory EventCategory { get; set; } = null!;

    public required string Title { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public required string VenueName { get; set; }

    public required string City { get; set; }

    public string? PostalCode { get; set; }

    public EventStatus Status { get; set; } = EventStatus.Draft;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<SavedEvent> SavedByUsers { get; set; } = [];
}
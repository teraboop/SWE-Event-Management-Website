using EventManagement.Data;

namespace EventManagement.Models.Events;


public enum TicketSalesMode
{
    Unconfigured = 0,
    OnPlatform = 1,
    External = 2,
    NotRequired = 3
}

public class EventTicketType
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public required string Name { get; set; }
    public decimal Price { get; set; }
    public int QuantityAvailable { get; set; }
    public DateTimeOffset SalesStartAt { get; set; }
    public DateTimeOffset SalesEndAt { get; set; }
    public bool IsEnabled { get; set; } = true;

}

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

    public decimal MinimumPrice { get; set; }

    public TicketSalesMode TicketSalesMode { get; set; } = TicketSalesMode.Unconfigured;

    public string? ExternalTicketingUrl { get; set; }

    public ICollection<EventTicketType> TicketTypes { get; set; } = [];

    public string? ExternalWebsiteUrl { get; set; }

    public ICollection<EventInterest> Interests { get; set; } = [];
}
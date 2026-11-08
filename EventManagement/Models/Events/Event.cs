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

    /// <summary>
    /// Total capacity for this ticket type. This does not go down as tickets
    /// sell; remaining tickets are <c>QuantityAvailable - QuantitySold</c>.
    /// </summary>
    public int QuantityAvailable { get; set; }

    /// <summary>
    /// Running count of tickets held by pending or completed orders. Only the
    /// purchase service changes it, with one atomic UPDATE, and a database
    /// check constraint keeps it within 0..QuantityAvailable, so tickets can
    /// never oversell, even with simultaneous buyers.
    /// </summary>
    public int QuantitySold { get; set; }

    public DateTimeOffset SalesStartAt { get; set; }
    public DateTimeOffset SalesEndAt { get; set; }
    public bool IsEnabled { get; set; } = true;

    public ICollection<Commerce.Ticket> Tickets { get; set; } = [];
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

    /// <summary>
    /// Organizer opt-in. Vendors can only apply to sell at events that have
    /// this turned on, so organizers don't get applications they didn't ask for.
    /// </summary>
    public bool AcceptsVendorApplications { get; set; }

    public ICollection<Vendors.VendorApplication> VendorApplications { get; set; } = [];

    /// <summary>
    /// Organizer opt-in for panel proposals, separate from vendor applications
    /// so an event can take one without the other.
    /// </summary>
    public bool AcceptsPanelApplications { get; set; }

    public ICollection<Panels.PanelApplication> PanelApplications { get; set; } = [];

    /// <summary>Optional background for the event page header and quick view.</summary>
    public int? BackgroundImageId { get; set; }

    public EventImage? BackgroundImage { get; set; }
}
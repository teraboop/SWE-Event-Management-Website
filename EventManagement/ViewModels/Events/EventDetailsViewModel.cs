using EventManagement.Models.Events;

namespace EventManagement.ViewModels.Events;

public class EventDetailsViewModel
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public string VenueName { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public string? PostalCode { get; set; }

    public decimal MinimumPrice { get; set; }

    public TicketSalesMode TicketSalesMode { get; set; }

    public string? ExternalTicketingUrl { get; set; }

    public IReadOnlyList<EventTicketTypeDetailsViewModel> AvailableTicketTypes { get; set; } = [];

    public bool HasStarted { get; set; }

    /// <summary>True when the signed-in user organizes this event.</summary>
    public bool IsOwnEvent { get; set; }

    /// <summary>Tickets the signed-in user already holds for this event.</summary>
    public int TicketsHeld { get; set; }

    public bool HasEnded { get; set; }

    public bool AcceptsVendorApplications { get; set; }

    /// <summary>Whether the signed-in user has a vendor profile to apply with.</summary>
    public bool HasVendorProfile { get; set; }

    /// <summary>The signed-in vendor's current application status for this event, if any.</summary>
    public EventManagement.Models.Applications.ApplicationStatus? VendorApplicationStatus { get; set; }

    /// <summary>Organizer updates, pinned first, then newest.</summary>
    public IReadOnlyList<EventAnnouncementItem> Announcements { get; set; } = [];

    /// <summary>Vendors approved to sell at this event, listed publicly.</summary>
    public IReadOnlyList<EventManagement.ViewModels.Shop.EventVendorViewModel> ApprovedVendors { get; set; } = [];

    public bool AcceptsPanelApplications { get; set; }

    /// <summary>Approved panels, listed publicly on the event page.</summary>
    public IReadOnlyList<EventManagement.ViewModels.Panels.EventPanelViewModel> ApprovedPanels { get; set; } = [];

    /// <summary>How many of the signed-in user's panel proposals for this event are pending.</summary>
    public int PendingPanelProposals { get; set; }

    public string? ExternalWebsiteUrl { get; set; }

    /// <summary>Background for the page header and quick view; served at /images/event/{id}.</summary>
    public int? BackgroundImageId { get; set; }

    public bool IsSaved { get; set; }

    public string ReturnUrl { get; set; } = "/Events";

    public string EventCategoryName { get; set; } = string.Empty;

    public string OrganizerName { get; set; } = string.Empty;

    public IReadOnlyList<string> Interests { get; set; } = [];

    /// <summary>This event's own rating. Only events that have ended can be reviewed.</summary>
    public EventManagement.ViewModels.Reviews.RatingSummary EventRating { get; set; } =
        EventManagement.ViewModels.Reviews.RatingSummary.Empty;

    /// <summary>Rating across all of this organizer's reviewed events.</summary>
    public EventManagement.ViewModels.Reviews.RatingSummary OrganizerRating { get; set; } =
        EventManagement.ViewModels.Reviews.RatingSummary.Empty;

    /// <summary>The Reviews section; loaded on the full page once the event has ended.</summary>
    public EventManagement.ViewModels.Reviews.EventReviewsViewModel? Reviews { get; set; }
}

public class EventTicketTypeDetailsViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public int Remaining { get; set; }

    public bool IsSoldOut => Remaining <= 0;

    public DateTimeOffset SalesStartAt { get; set; }

    public DateTimeOffset SalesEndAt { get; set; }
}

using EventManagement.Models.Events;

namespace EventManagement.ViewModels.Tickets;

public class MyTicketsViewModel
{
    public IReadOnlyList<MyTicketsEventViewModel> UpcomingEvents { get; set; } = [];

    public IReadOnlyList<MyTicketsEventViewModel> PastEvents { get; set; } = [];
}

/// <summary>All of a user's tickets for one event, grouped for display.</summary>
public class MyTicketsEventViewModel
{
    public int EventId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public string VenueName { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public EventStatus Status { get; set; }

    /// <summary>The event is over and wasn't cancelled, so the ticket holder can review it.</summary>
    public bool CanReview { get; set; }

    /// <summary>Stars the user gave this event, if they've reviewed it.</summary>
    public int? MyRating { get; set; }

    public IReadOnlyList<MyTicketViewModel> Tickets { get; set; } = [];
}

public class MyTicketViewModel
{
    public int OrderId { get; set; }

    public string TicketTypeName { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public Guid Code { get; set; }

    public DateTimeOffset PurchasedAt { get; set; }

    /// <summary>Short, human-readable form of the ticket code.</summary>
    public string DisplayCode => Code.ToString("N")[..10].ToUpperInvariant();
}

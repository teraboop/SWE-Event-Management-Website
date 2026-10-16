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

    public string? ExternalWebsiteUrl { get; set; }

    public string EventCategoryName { get; set; } = string.Empty;

    public string OrganizerName { get; set; } = string.Empty;

    public IReadOnlyList<string> Interests { get; set; } = [];
}
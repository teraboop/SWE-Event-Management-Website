namespace EventManagement.ViewModels.Events;

public class EventCardViewModel
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public string VenueName { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public decimal MinimumPrice { get; set; }

    public string? ExternalWebsiteUrl { get; set; }

    public bool IsSaved { get; set; }

    /// <summary>The organizer has opened vendor applications for this event.</summary>
    public bool AcceptsVendorApplications { get; set; }

    public IReadOnlyList<string> Interests { get; set; } = [];
}
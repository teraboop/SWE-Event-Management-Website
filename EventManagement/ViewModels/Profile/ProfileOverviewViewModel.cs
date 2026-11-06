namespace EventManagement.ViewModels.Profile;

public class ProfileOverviewViewModel
{
    public string DisplayName { get; set; } = string.Empty;

    public string? City { get; set; }

    public string? PostalCode { get; set; }

    public List<string> Interests { get; set; } = [];

    public List<SavedEventSummaryViewModel> SavedEvents { get; set; } = [];
}

public class SavedEventSummaryViewModel
{
    public int EventId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset StartsAt { get; set; }

    public string VenueName { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;
}
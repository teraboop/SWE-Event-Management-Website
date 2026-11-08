namespace EventManagement.ViewModels.Events;

public class EventFilterViewModel
{
    public int? InterestCategoryId { get; set; }

    public DateTimeOffset? StartsAfter { get; set; }

    public DateTimeOffset? StartsBefore { get; set; }

    public string? City { get; set; }

    public decimal? MinimumPrice { get; set; }

    public decimal? MaximumPrice { get; set; }

    /// <summary>Only show events that are accepting vendor applications.</summary>
    public bool OpenToVendors { get; set; }
}

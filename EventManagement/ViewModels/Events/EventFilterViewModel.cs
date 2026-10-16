namespace EventManagement.ViewModels.Events;

public class EventFilterViewModel
{
    public int? InterestCategoryId { get; set; }

    public DateTimeOffset? StartsAfter { get; set; }

    public DateTimeOffset? StartsBefore { get; set; }

    public string? City { get; set; }

    public decimal? MinimumPrice { get; set; }

    public decimal? MaximumPrice { get; set; }
}

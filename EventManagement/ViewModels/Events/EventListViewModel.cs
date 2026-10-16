using EventManagement.Models.Profile;

namespace EventManagement.ViewModels.Events;

public class EventListViewModel
{
    public EventFilterViewModel Filter { get; set; } = new();

    public IReadOnlyList<InterestCategory> InterestCategories { get; set; } = [];

    public IReadOnlyList<EventCardViewModel> Events { get; set; } = [];
}
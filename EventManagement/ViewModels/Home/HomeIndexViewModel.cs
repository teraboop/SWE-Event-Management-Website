using EventManagement.Models.Profile;
using EventManagement.ViewModels.Events;

namespace EventManagement.ViewModels.Home;

public class HomeIndexViewModel
{
    public IReadOnlyList<EventCardViewModel> PopularEvents { get; set; } = [];

    public IReadOnlyList<InterestCategory> InterestCategories { get; set; } = [];
}

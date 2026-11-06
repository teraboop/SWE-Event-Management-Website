namespace EventManagement.ViewModels.Profile;

public class InterestsViewModel
{
    public List<int> SelectedInterestIds { get; set; } = [];

    public List<InterestOptionViewModel> AvailableInterests { get; set; } = [];
}

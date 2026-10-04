using EventManagement.Data;

namespace EventManagement.Models.Profile;

public class UserInterest
{
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    public int InterestCategoryId { get; set; }

    public InterestCategory InterestCategory { get; set; } = null!;
}
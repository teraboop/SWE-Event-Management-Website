using EventManagement.Models.Profile;

namespace EventManagement.Models.Events;

public class EventInterest
{
    public int EventId { get; set; }

    public Event Event { get; set; } = null!;

    public int InterestCategoryId { get; set; }

    public InterestCategory InterestCategory { get; set; } = null!;
}
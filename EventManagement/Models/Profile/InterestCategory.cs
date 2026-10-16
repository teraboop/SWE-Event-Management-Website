using EventManagement.Models.Events;

namespace EventManagement.Models.Profile;

public class InterestCategory
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public ICollection<UserInterest> UserInterests { get; set; } = [];

    public ICollection<EventInterest> EventInterests { get; set; } = [];
}
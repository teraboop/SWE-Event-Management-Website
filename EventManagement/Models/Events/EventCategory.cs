namespace EventManagement.Models.Events;

public class EventCategory
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public ICollection<Event> Events { get; set; } = [];
}
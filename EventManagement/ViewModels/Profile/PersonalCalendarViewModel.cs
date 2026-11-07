namespace EventManagement.ViewModels.Profile;

public class PersonalCalendarViewModel
{
    public DateOnly MonthStart { get; set; }

    public DateOnly PreviousMonth { get; set; }

    public DateOnly NextMonth { get; set; }

    public List<List<CalendarDayViewModel>> Weeks { get; set; } = [];
}

public class CalendarDayViewModel
{
    public DateOnly Date { get; set; }

    public bool IsInDisplayedMonth { get; set; }

    public List<CalendarEventViewModel> Events { get; set; } = [];
}

public class CalendarEventViewModel
{
    public int EventId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }
}
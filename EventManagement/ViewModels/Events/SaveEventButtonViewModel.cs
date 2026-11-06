namespace EventManagement.ViewModels.Events;

public class SaveEventButtonViewModel
{
    public int EventId { get; set; }

    public bool IsSaved { get; set; }

    public string ReturnUrl { get; set; } = "/Events";
}
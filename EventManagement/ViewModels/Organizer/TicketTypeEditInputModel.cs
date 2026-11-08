using System.ComponentModel.DataAnnotations;

namespace EventManagement.ViewModels.Organizer;

public class TicketTypeEditInputModel
{
    public List<TicketTypeInputModel> TicketTypes { get; set; } = [];
}

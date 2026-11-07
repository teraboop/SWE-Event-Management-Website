using System.ComponentModel.DataAnnotations;
using EventManagement.Models.Events;

namespace EventManagement.ViewModels.Organizer;

public class TicketingSettingsInputModel
{
    public TicketSalesMode TicketSalesMode { get; set; }
        = TicketSalesMode.Unconfigured;

    [StringLength(2048)]
    public string? ExternalTicketingUrl { get; set; }

    public List<TicketTypeInputModel> TicketTypes { get; set; } = [];
}

public class TicketTypeInputModel
{
    public int? Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "10000000")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int QuantityAvailable { get; set; }

    public DateTimeOffset? SalesStartAt { get; set; }

    public DateTimeOffset? SalesEndAt { get; set; }

    public bool IsEnabled { get; set; } = true;
}
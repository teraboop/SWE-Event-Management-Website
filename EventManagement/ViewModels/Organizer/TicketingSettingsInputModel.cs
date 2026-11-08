using System.ComponentModel.DataAnnotations;
using EventManagement.Models.Events;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EventManagement.ViewModels.Organizer;

public class TicketingSettingsInputModel
{
    public TicketSalesMode TicketSalesMode { get; set; }
        = TicketSalesMode.Unconfigured;

    [StringLength(2048)]
    public string? ExternalTicketingUrl { get; set; }
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

    [Required]
    public DateTimeOffset SalesStartAt { get; set; }

    [Required]
    public DateTimeOffset SalesEndAt { get; set; }

    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Display only. Always loaded from the database and never bound from the
    /// form, so a tampered request can't change it.
    /// </summary>
    [BindNever]
    public int QuantitySold { get; set; }
}

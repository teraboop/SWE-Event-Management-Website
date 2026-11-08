using System.ComponentModel.DataAnnotations;

namespace EventManagement.ViewModels.Organizer;

public class NewTicketTypeInputModel
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "10000000")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int QuantityAvailable { get; set; }

    public DateTimeOffset SalesStartAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset SalesEndAt { get; set; } = DateTimeOffset.Now.AddDays(1);
}

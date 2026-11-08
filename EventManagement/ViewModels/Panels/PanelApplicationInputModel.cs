using System.ComponentModel.DataAnnotations;

namespace EventManagement.ViewModels.Panels;

public class PanelApplicationInputModel
{
    [Required(ErrorMessage = "Give your panel a title.")]
    [StringLength(150)]
    [Display(Name = "Panel title")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Describe what the panel will cover.")]
    [StringLength(2000)]
    [Display(Name = "What the panel is about")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "List who will be on the panel.")]
    [StringLength(1000)]
    [Display(Name = "Panelists")]
    public string Panelists { get; set; } = string.Empty;

    [Range(15, 240, ErrorMessage = "Choose a length between 15 minutes and 4 hours.")]
    [Display(Name = "Length")]
    public int DurationMinutes { get; set; } = 60;

    [Required(ErrorMessage = "Enter a contact email.")]
    [EmailAddress]
    [StringLength(256)]
    [Display(Name = "Contact email")]
    public string ContactEmail { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Note to the organizer (optional)")]
    public string? Message { get; set; }
}

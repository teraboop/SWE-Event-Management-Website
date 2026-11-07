using System.ComponentModel.DataAnnotations;

namespace EventManagement.ViewModels.Profile;

public class ProfileEditViewModel
{
    [Required]
    [StringLength(100)]
    [Display(Name = "Display name")]
    public string DisplayName { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Time zone")]
    public string? TimeZoneId { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(20)]
    [Display(Name = "Postal code")]
    public string? PostalCode { get; set; }
}
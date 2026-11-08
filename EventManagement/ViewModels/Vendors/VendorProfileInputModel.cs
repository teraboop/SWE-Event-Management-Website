using System.ComponentModel.DataAnnotations;

namespace EventManagement.ViewModels.Vendors;

public class VendorProfileInputModel
{
    [Required(ErrorMessage = "Enter your business name.")]
    [StringLength(150)]
    [Display(Name = "Business name")]
    public string BusinessName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Describe what you sell.")]
    [StringLength(500)]
    [Display(Name = "What you sell")]
    public string ProductsOffered { get; set; } = string.Empty;

    [Required(ErrorMessage = "Describe your business.")]
    [StringLength(2000)]
    [Display(Name = "About your business")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a contact email.")]
    [EmailAddress]
    [StringLength(256)]
    [Display(Name = "Contact email")]
    public string ContactEmail { get; set; } = string.Empty;

    [StringLength(2048)]
    [Display(Name = "Website (optional)")]
    public string? WebsiteUrl { get; set; }
}

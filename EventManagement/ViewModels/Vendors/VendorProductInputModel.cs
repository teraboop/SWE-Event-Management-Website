using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EventManagement.ViewModels.Vendors;

/// <summary>The vendor's add/edit product form.</summary>
public class VendorProductInputModel
{
    [Required(ErrorMessage = "Enter the product's name.")]
    [StringLength(150)]
    [Display(Name = "Product name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Describe the product.")]
    [StringLength(2000)]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a price.")]
    [Range(typeof(decimal), "0.01", "100000", ErrorMessage = "Enter a price between $0.01 and $100,000.")]
    [DataType(DataType.Currency)]
    [Display(Name = "Price")]
    public decimal? Price { get; set; }

    [Required(ErrorMessage = "Enter how many you have in stock.")]
    [Range(0, 100000, ErrorMessage = "Enter a number from 0 to 100,000.")]
    [Display(Name = "Quantity in stock")]
    public int? QuantityInStock { get; set; }

    [Display(Name = "Show this product in the shop")]
    public bool IsListed { get; set; } = true;

    [Display(Name = "Photo (optional)")]
    public IFormFile? Image { get; set; }
}

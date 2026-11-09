using EventManagement.Data;
using EventManagement.Services;
using EventManagement.ViewModels.Vendors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EventManagement.Areas.Vendor.Pages.Products;

/// <summary>Add a product (no id) or edit one of the vendor's own products.</summary>
[Authorize]
[RequestFormLimits(MultipartBodyLengthLimit = EventImageService.MaxBytes + 1024 * 1024)]
[RequestSizeLimit(EventImageService.MaxBytes + 1024 * 1024)]
public class EditModel : PageModel
{
    private readonly VendorProductService _productService;
    private readonly UserManager<ApplicationUser> _userManager;

    public EditModel(VendorProductService productService, UserManager<ApplicationUser> userManager)
    {
        _productService = productService;
        _userManager = userManager;
    }

    [BindProperty]
    public VendorProductInputModel Input { get; set; } = new();

    [BindProperty]
    public bool RemoveImage { get; set; }

    public int? ProductId { get; private set; }

    public int? CurrentImageId { get; private set; }

    public bool IsNew => ProductId is null;

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        var seller = await _productService.GetSellerStatusAsync(userId, HttpContext.RequestAborted);
        if (!seller.CanSell)
        {
            return RedirectToPage("./Index");
        }

        if (id is int productId)
        {
            var product = await _productService.GetOwnProductAsync(seller.Profile!.Id, productId, HttpContext.RequestAborted);
            if (product is null)
            {
                return NotFound();
            }

            ProductId = product.Id;
            CurrentImageId = product.ImageId;
            Input = new VendorProductInputModel
            {
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                QuantityInStock = product.QuantityInStock,
                IsListed = product.IsListed
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int? id)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            await LoadCurrentAsync(userId, id);
            return Page();
        }

        var result = await _productService.SaveAsync(userId, id, Input, RemoveImage, HttpContext.RequestAborted);
        if (!result.Succeeded)
        {
            var key = result.Field is null ? string.Empty : $"{nameof(Input)}.{result.Field}";
            ModelState.AddModelError(key, result.Message);
            await LoadCurrentAsync(userId, id);
            return Page();
        }

        TempData[nameof(IndexModel.StatusMessage)] = result.Message;
        return RedirectToPage("./Index");
    }

    /// <summary>Restores what the form shows besides the inputs (the current photo) after a failed post.</summary>
    private async Task LoadCurrentAsync(string userId, int? id)
    {
        ProductId = id;
        if (id is not int productId)
        {
            return;
        }

        var seller = await _productService.GetSellerStatusAsync(userId, HttpContext.RequestAborted);
        if (seller.Profile is not null)
        {
            var product = await _productService.GetOwnProductAsync(seller.Profile.Id, productId, HttpContext.RequestAborted);
            CurrentImageId = product?.ImageId;
        }
    }
}

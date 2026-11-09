using EventManagement.Data;
using EventManagement.Models.Vendors;
using EventManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EventManagement.Areas.Vendor.Pages.Products;

/// <summary>The vendor's product list: add, edit, show/hide and remove products.</summary>
[Authorize]
public class IndexModel : PageModel
{
    private readonly VendorProductService _productService;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(VendorProductService productService, UserManager<ApplicationUser> userManager)
    {
        _productService = productService;
        _userManager = userManager;
    }

    public SellerStatus Seller { get; private set; } = new(null, false);

    public IReadOnlyList<VendorProduct> Products { get; private set; } = [];

    /// <summary>Units sold per product id, from completed orders.</summary>
    public IReadOnlyDictionary<int, int> UnitsSold { get; private set; } = new Dictionary<int, int>();

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        Seller = await _productService.GetSellerStatusAsync(userId, HttpContext.RequestAborted);
        if (Seller.Profile is not null)
        {
            Products = await _productService.GetOwnProductsAsync(Seller.Profile.Id, HttpContext.RequestAborted);
            UnitsSold = await _productService.GetUnitsSoldAsync(Seller.Profile.Id, HttpContext.RequestAborted);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSetListedAsync(int productId, bool listed)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        Report(await _productService.SetListedAsync(userId, productId, listed, HttpContext.RequestAborted));
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAsync(int productId)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        Report(await _productService.ArchiveAsync(userId, userIsAdmin: false, productId, HttpContext.RequestAborted));
        return RedirectToPage();
    }

    private void Report(VendorProductResult result)
    {
        if (result.Succeeded)
        {
            StatusMessage = result.Message;
        }
        else
        {
            ErrorMessage = result.Message;
        }
    }
}

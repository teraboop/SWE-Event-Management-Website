using EventManagement.Data;
using EventManagement.Services;
using EventManagement.ViewModels.Shop;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EventManagement.Controllers;

/// <summary>The public marketplace: browse products, view one, and visit a vendor's storefront.</summary>
public class ShopController : Controller
{
    private readonly VendorProductService _productService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ShopController(VendorProductService productService, UserManager<ApplicationUser> userManager)
    {
        _productService = productService;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(ShopFilterViewModel filter)
    {
        var model = await _productService.GetShopAsync(filter, HttpContext.RequestAborted);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Product(int id)
    {
        var model = await _productService.GetProductAsync(id, _userManager.GetUserId(User), HttpContext.RequestAborted);
        return model is null ? NotFound() : View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Vendor(int id)
    {
        var model = await _productService.GetStorefrontAsync(id, _userManager.GetUserId(User), HttpContext.RequestAborted);
        return model is null ? NotFound() : View(model);
    }

    /// <summary>Admin moderation: takes a product out of the shop.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id)
    {
        var result = await _productService.ArchiveAsync(
            _userManager.GetUserId(User)!, userIsAdmin: true, id, HttpContext.RequestAborted);

        TempData["ShopMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}

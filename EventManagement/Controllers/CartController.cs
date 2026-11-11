using System.Globalization;
using EventManagement.Data;
using EventManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EventManagement.Controllers;

/// <summary>The cart and the faux checkout. The rules live in CartService and CheckoutService.</summary>
[Authorize]
public class CartController : Controller
{
    private readonly CartService _cartService;
    private readonly CheckoutService _checkoutService;
    private readonly RewardService _rewardService;
    private readonly UserManager<ApplicationUser> _userManager;

    public CartController(
        CartService cartService,
        CheckoutService checkoutService,
        RewardService rewardService,
        UserManager<ApplicationUser> userManager)
    {
        _rewardService = rewardService;
        _cartService = cartService;
        _checkoutService = checkoutService;
        _userManager = userManager;
    }

    private string UserId => _userManager.GetUserId(User)!;

    [HttpGet]
    public async Task<IActionResult> Index() =>
        View(await _cartService.GetCartAsync(UserId, HttpContext.RequestAborted));

    /// <summary>From the ticket table on an event page.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTickets(int eventId, int ticketTypeId, int quantity)
    {
        var result = await _cartService.AddTicketsAsync(UserId, eventId, ticketTypeId, quantity, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            TempData["TicketError"] = result.Message;
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        TempData["CartMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>From a product page in the shop.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProduct(int productId, int quantity)
    {
        var result = await _cartService.AddProductAsync(UserId, productId, quantity, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            TempData["ShopError"] = result.Message;
            return RedirectToAction("Product", "Shop", new { id = productId });
        }

        TempData["CartMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int lineId, int quantity)
    {
        Report(await _cartService.UpdateQuantityAsync(UserId, lineId, quantity, HttpContext.RequestAborted));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int lineId)
    {
        Report(await _cartService.RemoveAsync(UserId, lineId, HttpContext.RequestAborted));
        return RedirectToAction(nameof(Index));
    }

    /// <summary>The review page before placing the order.</summary>
    [HttpGet]
    public async Task<IActionResult> Checkout()
    {
        var cart = await _cartService.GetCartAsync(UserId, HttpContext.RequestAborted);
        if (!cart.CanCheckout)
        {
            if (!cart.IsEmpty)
            {
                TempData["CartError"] = "Some items in your cart need attention before you can check out.";
            }

            return RedirectToAction(nameof(Index));
        }

        var balance = await _rewardService.GetBalanceAsync(UserId, HttpContext.RequestAborted);

        return View(new ViewModels.Rewards.CheckoutViewModel
        {
            Cart = cart,
            PointsBalance = balance,
            MaxRedeemablePoints = RewardService.MaxRedeemablePoints(balance, cart.Total)
        });
    }

    /// <param name="pointsToRedeem">Points to spend for a discount; 0 for none.</param>
    /// <param name="expectedTotal">The total shown on the checkout page, written with a "." decimal point.</param>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder(string? expectedTotal, int pointsToRedeem = 0)
    {
        if (!decimal.TryParse(expectedTotal, NumberStyles.Number, CultureInfo.InvariantCulture, out var total))
        {
            return RedirectToAction(nameof(Checkout));
        }

        var result = await _checkoutService.PlaceOrderAsync(UserId, total, pointsToRedeem, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            TempData["CartError"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        TempData["OrderMessage"] = "Thanks! Your order is confirmed.";
        return RedirectToAction("Details", "Orders", new { id = result.OrderId });
    }

    private void Report(CartResult result) =>
        TempData[result.Succeeded ? "CartMessage" : "CartError"] = result.Message;
}

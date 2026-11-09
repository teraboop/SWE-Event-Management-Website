using EventManagement.Data;
using EventManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EventManagement.Controllers;

/// <summary>
/// Form posts from the Reviews section of the event page. Both actions send
/// the user back to that section with a message; the rules live in
/// EventReviewService.
/// </summary>
[Authorize]
public class ReviewsController : Controller
{
    private readonly EventReviewService _eventReviewService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReviewsController(EventReviewService eventReviewService, UserManager<ApplicationUser> userManager)
    {
        _eventReviewService = eventReviewService;
        _userManager = userManager;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int eventId, int? rating, string? comment)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        if (rating is null)
        {
            TempData["ReviewError"] = "Choose a star rating.";
            return BackToReviews(eventId);
        }

        var result = await _eventReviewService.SaveAsync(
            userId, eventId, rating.Value, comment, HttpContext.RequestAborted);

        TempData[result.Succeeded ? "ReviewMessage" : "ReviewError"] = result.Message;
        return BackToReviews(eventId);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        var result = await _eventReviewService.DeleteAsync(
            userId, User.IsInRole("Admin"), id, HttpContext.RequestAborted);

        TempData[result.Succeeded ? "ReviewMessage" : "ReviewError"] = result.Message;

        return result.EventId is int eventId
            ? BackToReviews(eventId)
            : RedirectToAction("Index", "Events");
    }

    private IActionResult BackToReviews(int eventId) =>
        RedirectToAction("Details", "Events", new { id = eventId }, "reviews");
}

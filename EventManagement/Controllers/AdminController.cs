using EventManagement.Data;
using EventManagement.Models.Applications;
using EventManagement.Models.Vendors;
using EventManagement.Services;
using EventManagement.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly VendorApplicationService _vendorApplicationService;
    private readonly EventApplicationService _eventApplicationService;

    public AdminController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        VendorApplicationService vendorApplicationService,
        EventApplicationService eventApplicationService)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _vendorApplicationService = vendorApplicationService;
        _eventApplicationService = eventApplicationService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var organizerIds = (await _userManager.GetUsersInRoleAsync("Organizer"))
            .Select(user => user.Id)
            .ToHashSet();

        var users = await _userManager.Users
            .OrderBy(user => user.UserName)
            .ToListAsync();

        var model = new AdminIndexViewModel
        {
            PendingOnlineVendorApplications = await _dbContext.VendorApplications.CountAsync(application =>
                application.Kind == VendorApplicationKind.OnlineSales &&
                application.Status == ApplicationStatus.Submitted),
            PendingEventApplications = await _dbContext.EventApplications.CountAsync(application =>
                application.Status == ApplicationStatus.Submitted),
            Users = users
                .Select(user => new AdminUserRowViewModel
                {
                    Id = user.Id,
                    DisplayName = user.DisplayName,
                    UserName = user.UserName,
                    Email = user.Email,
                    IsOrganizer = organizerIds.Contains(user.Id)
                })
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetOrganizerRole(string userId, bool makeOrganizer)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return BadRequest();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        var isOrganizer = await _userManager.IsInRoleAsync(user, "Organizer");
        if (isOrganizer == makeOrganizer)
        {
            TempData["StatusMessage"] = "No role change was needed.";
            return RedirectToAction(nameof(Index));
        }

        var result = makeOrganizer
            ? await _userManager.AddToRoleAsync(user, "Organizer")
            : await _userManager.RemoveFromRoleAsync(user, "Organizer");

        TempData["StatusMessage"] = result.Succeeded
            ? makeOrganizer
                ? $"{user.UserName} is now an Organizer. They may need to sign in again for the role to appear in their current session."
                : $"The Organizer role was removed from {user.UserName}. They may need to sign in again for the change to appear in their current session."
            : string.Join(" ", result.Errors.Select(error => error.Description));

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Review queue for applications to sell merchandise on the website.</summary>
    [HttpGet]
    public async Task<IActionResult> VendorApplications()
    {
        var reviewAction = Url.Action(nameof(ReviewVendorApplication)) ?? string.Empty;
        var applications = await _vendorApplicationService.GetReviewQueueAsync(null, HttpContext.RequestAborted);
        applications.ForEach(application => application.ReviewFormAction = reviewAction);

        return View(applications);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewVendorApplication(int applicationId, bool approve, string? note)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        var result = await _vendorApplicationService.ReviewAsync(
            userId,
            _userManager.GetUserName(User) ?? "Admin",
            reviewerIsAdmin: true,
            applicationId,
            approve,
            note,
            HttpContext.RequestAborted);

        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(VendorApplications));
    }

    /// <summary>Review queue for applications to list new events.</summary>
    [HttpGet]
    public async Task<IActionResult> EventApplications()
    {
        var reviewAction = Url.Action(nameof(ReviewEventApplication)) ?? string.Empty;
        var applications = await _eventApplicationService.GetReviewQueueAsync(HttpContext.RequestAborted);
        applications.ForEach(application => application.ReviewFormAction = reviewAction);

        return View(applications);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewEventApplication(int applicationId, bool approve, string? note)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        var result = await _eventApplicationService.ReviewAsync(
            userId,
            _userManager.GetUserName(User) ?? "Admin",
            reviewerIsAdmin: true,
            applicationId,
            approve,
            note,
            HttpContext.RequestAborted);

        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAction(nameof(EventApplications));
    }
}
using EventManagement.Data;
using EventManagement.Models.Applications;
using EventManagement.Models.Vendors;
using EventManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Areas.Vendor.Pages.Applications;

/// <summary>The vendor's home: profile summary, online-sales status, and every application with its outcome.</summary>
[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly VendorApplicationService _applicationService;

    public IndexModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        VendorApplicationService applicationService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _applicationService = applicationService;
    }

    public VendorProfile? Profile { get; private set; }

    public ApplicationStatus? OnlineSalesStatus { get; private set; }

    public IReadOnlyList<ApplicationRow> Applications { get; private set; } = [];

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

        Profile = await _applicationService.GetProfileAsync(userId, HttpContext.RequestAborted);
        if (Profile is null)
        {
            return Page();
        }

        OnlineSalesStatus = await _applicationService.LatestStatusAsync(
            Profile.Id, VendorApplicationKind.OnlineSales, null, HttpContext.RequestAborted);

        Applications = await _dbContext.VendorApplications
            .AsNoTracking()
            .Where(application => application.VendorProfileId == Profile.Id)
            .OrderByDescending(application => application.SubmittedAt)
            .Select(application => new ApplicationRow
            {
                Id = application.Id,
                Kind = application.Kind,
                EventId = application.EventId,
                EventTitle = application.Event != null ? application.Event.Title : null,
                EventStartsAt = application.Event != null ? application.Event.StartsAt : null,
                Status = application.Status,
                SubmittedAt = application.SubmittedAt,
                DecidedAt = application.DecidedAt,
                ReviewNote = application.ReviewNote
            })
            .ToListAsync(HttpContext.RequestAborted);

        return Page();
    }

    public async Task<IActionResult> OnPostWithdrawAsync(int applicationId)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        var result = await _applicationService.WithdrawAsync(userId, applicationId, HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            StatusMessage = result.Message;
        }
        else
        {
            ErrorMessage = result.Message;
        }

        return RedirectToPage();
    }

    public class ApplicationRow
    {
        public int Id { get; set; }

        public VendorApplicationKind Kind { get; set; }

        public int? EventId { get; set; }

        public string? EventTitle { get; set; }

        public DateTimeOffset? EventStartsAt { get; set; }

        public ApplicationStatus Status { get; set; }

        public DateTimeOffset SubmittedAt { get; set; }

        public DateTimeOffset? DecidedAt { get; set; }

        public string? ReviewNote { get; set; }
    }
}

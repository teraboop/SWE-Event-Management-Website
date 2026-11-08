using EventManagement.Data;
using EventManagement.Models.Applications;
using EventManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Areas.Hosting.Pages;

/// <summary>/Hosting: the user's event applications and their outcomes.</summary>
[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly EventApplicationService _applicationService;

    public IndexModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        EventApplicationService applicationService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _signInManager = signInManager;
        _applicationService = applicationService;
    }

    public IReadOnlyList<ApplicationRow> Applications { get; private set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(bool refreshed = false)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        // Approval gives the applicant the Organizer role, but their sign-in
        // cookie still holds the old roles until it's reissued. Reissue it
        // here, so organizer tools work without signing out and back in.
        // The redirect is marked so this can't loop.
        if (!refreshed &&
            !User.IsInRole(EventApplicationService.OrganizerRole) &&
            await _userManager.IsInRoleAsync(user, EventApplicationService.OrganizerRole))
        {
            await _signInManager.RefreshSignInAsync(user);
            return RedirectToPage(new { refreshed = true });
        }

        Applications = await _dbContext.EventApplications
            .AsNoTracking()
            .Where(application => application.ApplicantUserId == user.Id)
            .OrderByDescending(application => application.SubmittedAt)
            .Select(application => new ApplicationRow
            {
                Id = application.Id,
                Title = application.Title,
                StartsAt = application.StartsAt,
                City = application.City,
                Status = application.Status,
                SubmittedAt = application.SubmittedAt,
                ReviewNote = application.ReviewNote,
                CreatedEventId = application.CreatedEventId
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

        public string Title { get; set; } = string.Empty;

        public DateTimeOffset StartsAt { get; set; }

        public string City { get; set; } = string.Empty;

        public ApplicationStatus Status { get; set; }

        public DateTimeOffset SubmittedAt { get; set; }

        public string? ReviewNote { get; set; }

        public int? CreatedEventId { get; set; }
    }
}

using EventManagement.Data;
using EventManagement.Models.Applications;
using EventManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Areas.Panels.Pages;

/// <summary>/Panels: every panel the user has proposed, with its outcome.</summary>
[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PanelApplicationService _applicationService;

    public IndexModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        PanelApplicationService applicationService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _applicationService = applicationService;
    }

    public IReadOnlyList<ProposalRow> Proposals { get; private set; } = [];

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

        Proposals = await _dbContext.PanelApplications
            .AsNoTracking()
            .Where(application => application.ApplicantUserId == userId)
            .OrderByDescending(application => application.SubmittedAt)
            .Select(application => new ProposalRow
            {
                Id = application.Id,
                Title = application.Title,
                EventId = application.EventId,
                EventTitle = application.Event.Title,
                EventStartsAt = application.Event.StartsAt,
                Status = application.Status,
                SubmittedAt = application.SubmittedAt,
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

    public class ProposalRow
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public int EventId { get; set; }

        public string EventTitle { get; set; } = string.Empty;

        public DateTimeOffset EventStartsAt { get; set; }

        public ApplicationStatus Status { get; set; }

        public DateTimeOffset SubmittedAt { get; set; }

        public string? ReviewNote { get; set; }
    }
}

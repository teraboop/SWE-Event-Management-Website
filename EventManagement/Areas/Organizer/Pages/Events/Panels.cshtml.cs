using EventManagement.Data;
using EventManagement.Models.Events;
using EventManagement.Services;
using EventManagement.ViewModels.Panels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Areas.Organizer.Pages.Events;

/// <summary>
/// Per-event panel management for the event's organizer (or an admin):
/// turn panel proposals on or off, and approve or reject them.
/// </summary>
[Authorize(Roles = "Organizer,Admin")]
public class PanelsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PanelApplicationService _applicationService;

    public PanelsModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        PanelApplicationService applicationService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _applicationService = applicationService;
    }

    public int EventId { get; private set; }

    public string EventTitle { get; private set; } = string.Empty;

    public bool AcceptsPanelApplications { get; private set; }

    public IReadOnlyList<PanelApplicationReviewItem> Applications { get; private set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var eventEntity = await _dbContext.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(@event => @event.Id == id);

        if (eventEntity is null)
        {
            return NotFound();
        }

        if (!CanManage(eventEntity))
        {
            return Forbid();
        }

        EventId = eventEntity.Id;
        EventTitle = eventEntity.Title;
        AcceptsPanelApplications = eventEntity.AcceptsPanelApplications;

        var reviewAction = Url.Page("./Panels", "Review", new { id }) ?? string.Empty;
        var applications = await _applicationService.GetReviewQueueAsync(id, HttpContext.RequestAborted);
        applications.ForEach(application => application.ReviewFormAction = reviewAction);
        Applications = applications;

        return Page();
    }

    public async Task<IActionResult> OnPostSetAcceptingAsync(int id, bool accepting)
    {
        var eventEntity = await _dbContext.Events.SingleOrDefaultAsync(@event => @event.Id == id);
        if (eventEntity is null)
        {
            return NotFound();
        }

        if (!CanManage(eventEntity))
        {
            return Forbid();
        }

        eventEntity.AcceptsPanelApplications = accepting;
        await _dbContext.SaveChangesAsync();

        StatusMessage = accepting
            ? "Attendees can now propose panels for this event."
            : "This event is no longer accepting panel proposals. Pending proposals can still be reviewed.";

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReviewAsync(int id, int applicationId, bool approve, string? note)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        // The proposal must belong to this page's event, so one event's page
        // can't be used to review another event's proposals.
        var belongsToEvent = await _dbContext.PanelApplications
            .AnyAsync(application => application.Id == applicationId && application.EventId == id);
        if (!belongsToEvent)
        {
            return NotFound();
        }

        // The service checks whether this user may review it.
        var result = await _applicationService.ReviewAsync(
            userId,
            _userManager.GetUserName(User) ?? "Organizer",
            User.IsInRole("Admin"),
            applicationId,
            approve,
            note,
            HttpContext.RequestAborted);

        if (result.Succeeded)
        {
            StatusMessage = result.Message;
        }
        else
        {
            ErrorMessage = result.Message;
        }

        return RedirectToPage(new { id });
    }

    private bool CanManage(Event eventEntity) =>
        User.IsInRole("Admin") || eventEntity.OrganizerId == _userManager.GetUserId(User);
}

using EventManagement.Data;
using EventManagement.Models.Events;
using EventManagement.Services;
using EventManagement.ViewModels.Vendors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Areas.Organizer.Pages.Events;

/// <summary>
/// Per-event vendor management for the event's organizer (or an admin):
/// turn vendor applications on or off, and approve or reject them.
/// </summary>
[Authorize(Roles = "Organizer,Admin")]
public class VendorsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly VendorApplicationService _applicationService;

    public VendorsModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        VendorApplicationService applicationService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _applicationService = applicationService;
    }

    public int EventId { get; private set; }

    public string EventTitle { get; private set; } = string.Empty;

    public bool AcceptsVendorApplications { get; private set; }

    public IReadOnlyList<VendorApplicationReviewItem> Applications { get; private set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var eventEntity = await FindManageableEventAsync(id);
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
        AcceptsVendorApplications = eventEntity.AcceptsVendorApplications;

        var reviewAction = Url.Page("./Vendors", "Review", new { id }) ?? string.Empty;
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

        eventEntity.AcceptsVendorApplications = accepting;
        await _dbContext.SaveChangesAsync();

        StatusMessage = accepting
            ? "Vendors can now apply to sell at this event."
            : "This event is no longer accepting vendor applications. Pending applications can still be reviewed.";

        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReviewAsync(int id, int applicationId, bool approve, string? note)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        // The application must belong to this page's event, so a request
        // can't use one event's page to review another event's applications.
        var belongsToEvent = await _dbContext.VendorApplications
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

    private Task<Event?> FindManageableEventAsync(int id) =>
        _dbContext.Events
            .AsNoTracking()
            .SingleOrDefaultAsync(@event => @event.Id == id);

    private bool CanManage(Event eventEntity) =>
        User.IsInRole("Admin") || eventEntity.OrganizerId == _userManager.GetUserId(User);
}

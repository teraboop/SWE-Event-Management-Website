using EventManagement.Data;
using EventManagement.Models.Events;
using EventManagement.Services;
using EventManagement.ViewModels.Panels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Areas.Panels.Pages;

/// <summary>/Panels/Apply?eventId=5: propose a panel for event 5.</summary>
[Authorize]
public class ApplyModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PanelApplicationService _applicationService;

    public ApplyModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        PanelApplicationService applicationService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _applicationService = applicationService;
    }

    [BindProperty(SupportsGet = true)]
    public int EventId { get; set; }

    [BindProperty]
    public PanelApplicationInputModel Input { get; set; } = new();

    public EventSummary TargetEvent { get; private set; } = null!;

    /// <summary>
    /// Why this user can't propose right now (event closed, own event, too
    /// many pending), or null when the form should be shown. The service
    /// checks the same rules again on submit.
    /// </summary>
    public string? BlockedReason { get; private set; }

    public int PendingCount { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var setup = await LoadAsync();
        if (setup is not null)
        {
            return setup;
        }

        // Start the contact email from the account's email; they can change it.
        var user = await _userManager.GetUserAsync(User);
        Input.ContactEmail = user?.Email ?? string.Empty;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var setup = await LoadAsync();
        if (setup is not null)
        {
            return setup;
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var userId = _userManager.GetUserId(User)!;
        var result = await _applicationService.SubmitAsync(userId, EventId, Input, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return Page();
        }

        StatusMessage = $"Your panel \"{Input.Title.Trim()}\" was submitted. The organizer of {TargetEvent.Title} will review it.";
        return RedirectToPage("./Index");
    }

    /// <summary>Loads the event and whether the user may propose. Returns a result to send instead of the page when the event doesn't exist.</summary>
    private async Task<IActionResult?> LoadAsync()
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        var targetEvent = await _dbContext.Events
            .AsNoTracking()
            .Where(@event => @event.Id == EventId && @event.Status == EventStatus.Published)
            .Select(@event => new EventSummary(
                @event.Id,
                @event.Title,
                @event.StartsAt,
                @event.VenueName,
                @event.City))
            .SingleOrDefaultAsync(HttpContext.RequestAborted);

        if (targetEvent is null)
        {
            return NotFound();
        }

        TargetEvent = targetEvent;
        PendingCount = await _applicationService.CountPendingAsync(userId, EventId, HttpContext.RequestAborted);

        BlockedReason = await _applicationService.CheckEventAcceptsProposalsAsync(userId, EventId, HttpContext.RequestAborted);
        if (BlockedReason is null && PendingCount >= PanelApplicationService.MaxPendingPerEvent)
        {
            BlockedReason = $"You already have {PendingCount} proposals waiting for review at this event. Withdraw one or wait for a decision before proposing another.";
        }

        return null;
    }

    public sealed record EventSummary(
        int Id,
        string Title,
        DateTimeOffset StartsAt,
        string VenueName,
        string City);
}

using EventManagement.Data;
using EventManagement.Models.Events;
using EventManagement.Services;
using EventManagement.ViewModels.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Areas.Organizer.Pages.Events;

/// <summary>
/// The organizer (or an admin) posts, edits, pins and deletes the
/// announcements shown on their event's page.
/// </summary>
[Authorize(Roles = "Organizer,Admin")]
public class AnnouncementsModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly EventAnnouncementService _announcementService;

    public AnnouncementsModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        EventAnnouncementService announcementService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _announcementService = announcementService;
    }

    /// <summary>The new-announcement form. Handlers take it as a parameter, so each form posts only its own fields.</summary>
    public EventAnnouncementInputModel Input { get; set; } = new();

    public int EventId { get; private set; }

    public string EventTitle { get; private set; } = string.Empty;

    public EventStatus Status { get; private set; }

    public IReadOnlyList<EventAnnouncementItem> Announcements { get; private set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id) =>
        await LoadAsync(id) ? Page() : NotFound();

    public async Task<IActionResult> OnPostAsync(int id, EventAnnouncementInputModel input)
    {
        if (!ModelState.IsValid)
        {
            Input = input;
            return await LoadAsync(id) ? Page() : NotFound();
        }

        var result = await _announcementService.PostAsync(
            UserId,
            _userManager.GetUserName(User) ?? "Organizer",
            IsAdmin,
            id,
            input,
            HttpContext.RequestAborted);

        return Report(result, id);
    }

    public async Task<IActionResult> OnPostEditAsync(int id, int announcementId, EventAnnouncementInputModel input)
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = ModelState.Values.SelectMany(entry => entry.Errors).First().ErrorMessage;
            return RedirectToPage(new { id });
        }

        var result = await _announcementService.UpdateAsync(
            UserId, IsAdmin, id, announcementId, input, HttpContext.RequestAborted);

        return Report(result, id);
    }

    public async Task<IActionResult> OnPostPinAsync(int id, int announcementId, bool pinned)
    {
        var result = await _announcementService.SetPinnedAsync(
            UserId, IsAdmin, id, announcementId, pinned, HttpContext.RequestAborted);

        return Report(result, id);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, int announcementId)
    {
        var result = await _announcementService.DeleteAsync(
            UserId, IsAdmin, id, announcementId, HttpContext.RequestAborted);

        return Report(result, id);
    }

    private string UserId => _userManager.GetUserId(User)!;

    private bool IsAdmin => User.IsInRole("Admin");

    /// <summary>Loads the event and its announcements; false when it doesn't exist or isn't the user's.</summary>
    private async Task<bool> LoadAsync(int id)
    {
        var userId = UserId;
        var isAdmin = IsAdmin;

        var eventInfo = await _dbContext.Events
            .AsNoTracking()
            .Where(@event => @event.Id == id && (isAdmin || @event.OrganizerId == userId))
            .Select(@event => new { @event.Id, @event.Title, @event.Status })
            .SingleOrDefaultAsync(HttpContext.RequestAborted);

        if (eventInfo is null)
        {
            return false;
        }

        EventId = eventInfo.Id;
        EventTitle = eventInfo.Title;
        Status = eventInfo.Status;
        Announcements = await _announcementService.GetForEventAsync(id, HttpContext.RequestAborted);
        return true;
    }

    private IActionResult Report(EventAnnouncementResult result, int id)
    {
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
}

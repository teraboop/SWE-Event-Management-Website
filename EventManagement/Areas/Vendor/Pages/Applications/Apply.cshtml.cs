using EventManagement.Data;
using EventManagement.Models.Vendors;
using EventManagement.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace EventManagement.Areas.Vendor.Pages.Applications;

/// <summary>
/// One page for both kinds of application: /Vendor/Applications/Apply sells
/// online, and /Vendor/Applications/Apply?eventId=5 sells at event 5. The
/// business details come from the vendor's profile and are shown read-only,
/// so applying is just an optional message and a submit button.
/// </summary>
[Authorize]
public class ApplyModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly VendorApplicationService _applicationService;

    public ApplyModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        VendorApplicationService applicationService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _applicationService = applicationService;
    }

    [BindProperty(SupportsGet = true)]
    public int? EventId { get; set; }

    [BindProperty]
    [StringLength(VendorApplicationService.MaxMessageLength)]
    [Display(Name = "Message to the reviewer (optional)")]
    public string? Message { get; set; }

    public VendorProfile Profile { get; private set; } = null!;

    public EventSummary? TargetEvent { get; private set; }

    /// <summary>
    /// The vendor's current status for this kind of application, so the page
    /// can say "already pending" or "already approved" instead of showing a
    /// form that would be refused. The service still enforces it on submit.
    /// </summary>
    public Models.Applications.ApplicationStatus? CurrentStatus { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var setup = await LoadAsync();
        return setup ?? Page();
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

        var result = await _applicationService.SubmitAsync(
            Profile.OwnerUserId, EventId, Message, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return Page();
        }

        StatusMessage = TargetEvent is null
            ? "Your application to sell online was submitted. An admin will review it."
            : $"Your application to sell at {TargetEvent.Title} was submitted. The organizer will review it.";

        return RedirectToPage("./Index");
    }

    /// <summary>
    /// Loads the profile and event. Returns a result to send back instead of
    /// the page when the user has no profile yet or the event doesn't exist.
    /// </summary>
    private async Task<IActionResult?> LoadAsync()
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        var profile = await _applicationService.GetProfileAsync(userId, HttpContext.RequestAborted);
        if (profile is null)
        {
            // Send them to create a profile, then bring them back here.
            return RedirectToPage("/Profile", new { returnUrl = Request.Path + Request.QueryString });
        }

        Profile = profile;

        if (EventId is int eventId)
        {
            TargetEvent = await _dbContext.Events
                .AsNoTracking()
                .Where(@event => @event.Id == eventId && @event.Status == Models.Events.EventStatus.Published)
                .Select(@event => new EventSummary(
                    @event.Id,
                    @event.Title,
                    @event.StartsAt,
                    @event.VenueName,
                    @event.City,
                    @event.AcceptsVendorApplications && @event.EndsAt > DateTimeOffset.UtcNow))
                .SingleOrDefaultAsync(HttpContext.RequestAborted);

            if (TargetEvent is null)
            {
                return NotFound();
            }
        }

        CurrentStatus = await _applicationService.LatestStatusAsync(
            profile.Id,
            EventId is null ? VendorApplicationKind.OnlineSales : VendorApplicationKind.Event,
            EventId,
            HttpContext.RequestAborted);

        return null;
    }

    public sealed record EventSummary(
        int Id,
        string Title,
        DateTimeOffset StartsAt,
        string VenueName,
        string City,
        bool IsAcceptingApplications);
}

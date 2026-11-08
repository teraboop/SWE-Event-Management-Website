using EventManagement.Data;
using EventManagement.Services;
using EventManagement.ViewModels.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EventManagement.Areas.Hosting.Pages;

/// <summary>/Hosting/Apply: apply to list a new event on the website.</summary>
[Authorize]
[RequestFormLimits(MultipartBodyLengthLimit = EventImageService.MaxBytes + 1024 * 1024)]
[RequestSizeLimit(EventImageService.MaxBytes + 1024 * 1024)]
public class ApplyModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly EventApplicationService _applicationService;

    public ApplyModel(UserManager<ApplicationUser> userManager, EventApplicationService applicationService)
    {
        _userManager = userManager;
        _applicationService = applicationService;
    }

    [BindProperty]
    public EventApplicationInputModel Input { get; set; } = new();

    public EventListingFormOptions ListingOptions { get; private set; } = null!;

    public int PendingCount { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        await LoadAsync(user.Id);

        // Start the contact details from the account; they can change them.
        Input.ContactName = user.DisplayName ?? string.Empty;
        Input.ContactEmail = user.Email ?? string.Empty;
        Input.ContactPhone = user.PhoneNumber;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        await LoadAsync(userId);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _applicationService.SubmitAsync(userId, Input, HttpContext.RequestAborted);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return Page();
        }

        StatusMessage = $"Your application to list \"{Input.Title.Trim()}\" was submitted. An admin will review it.";
        return RedirectToPage("./Index");
    }

    private async Task LoadAsync(string userId)
    {
        ListingOptions = await _applicationService.GetListingFormOptionsAsync(cancellationToken: HttpContext.RequestAborted);
        PendingCount = await _applicationService.CountPendingAsync(userId, HttpContext.RequestAborted);
    }
}

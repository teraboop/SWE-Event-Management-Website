using EventManagement.Data;
using EventManagement.Models.Vendors;
using EventManagement.ViewModels.Vendors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Areas.Vendor.Pages;

/// <summary>
/// Create or edit the signed-in user's vendor profile. Any signed-in user can
/// create one. Becoming a vendor doesn't need approval; selling does, and
/// that's what the applications are for.
/// </summary>
[Authorize]
public class ProfileModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TimeProvider _timeProvider;

    public ProfileModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _timeProvider = timeProvider;
    }

    [BindProperty]
    public VendorProfileInputModel Input { get; set; } = new();

    /// <summary>Where to continue afterwards, e.g. back to an Apply page.</summary>
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public bool IsNew { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        var profile = await _dbContext.VendorProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.OwnerUserId == userId);

        IsNew = profile is null;

        if (profile is not null)
        {
            Input = new VendorProfileInputModel
            {
                BusinessName = profile.BusinessName,
                ProductsOffered = profile.ProductsOffered,
                Description = profile.Description,
                ContactEmail = profile.ContactEmail,
                WebsiteUrl = profile.WebsiteUrl
            };
        }
        else
        {
            // Start from the account email; vendors can change it.
            var user = await _userManager.GetUserAsync(User);
            Input.ContactEmail = user?.Email ?? string.Empty;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        var profile = await _dbContext.VendorProfiles
            .SingleOrDefaultAsync(profile => profile.OwnerUserId == userId);

        IsNew = profile is null;

        var websiteUrl = string.IsNullOrWhiteSpace(Input.WebsiteUrl) ? null : Input.WebsiteUrl.Trim();
        if (websiteUrl is not null &&
            (!Uri.TryCreate(websiteUrl, UriKind.Absolute, out var websiteUri) ||
             (websiteUri.Scheme != Uri.UriSchemeHttps && websiteUri.Scheme != Uri.UriSchemeHttp)))
        {
            ModelState.AddModelError(
                $"{nameof(Input)}.{nameof(Input.WebsiteUrl)}",
                "Enter a valid link beginning with http:// or https://.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var now = _timeProvider.GetUtcNow();

        if (profile is null)
        {
            profile = new VendorProfile
            {
                OwnerUserId = userId,
                BusinessName = string.Empty,
                ProductsOffered = string.Empty,
                Description = string.Empty,
                ContactEmail = string.Empty,
                CreatedAt = now
            };
            _dbContext.VendorProfiles.Add(profile);
        }

        profile.BusinessName = Input.BusinessName.Trim();
        profile.ProductsOffered = Input.ProductsOffered.Trim();
        profile.Description = Input.Description.Trim();
        profile.ContactEmail = Input.ContactEmail.Trim();
        profile.WebsiteUrl = websiteUrl;
        profile.UpdatedAt = now;

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException) when (IsNew)
        {
            // The same user created a profile in another tab at the same time;
            // the unique index on the owner kept it to one.
            ModelState.AddModelError(string.Empty, "You already have a vendor profile. Reload the page to edit it.");
            return Page();
        }

        StatusMessage = IsNew
            ? "Vendor profile created. You can now apply to sell online or at events."
            : "Vendor profile saved. Pending applications keep the details you originally submitted.";

        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return LocalRedirect(ReturnUrl);
        }

        return RedirectToPage("/Applications/Index");
    }
}

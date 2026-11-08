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

/// <summary>The organizer (or an admin) edits an event's public listing, including its background image.</summary>
[Authorize(Roles = "Organizer,Admin")]
[RequestFormLimits(MultipartBodyLengthLimit = EventImageService.MaxBytes + 1024 * 1024)]
[RequestSizeLimit(EventImageService.MaxBytes + 1024 * 1024)]
public class EditModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly EventApplicationService _applicationService;
    private readonly EventImageService _imageService;

    public EditModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        EventApplicationService applicationService,
        EventImageService imageService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _applicationService = applicationService;
        _imageService = imageService;
    }

    [BindProperty]
    public EventListingInputModel Input { get; set; } = new();

    [BindProperty]
    public bool RemoveBackgroundImage { get; set; }

    public int EventId { get; private set; }

    public EventStatus Status { get; private set; }

    public EventListingFormOptions ListingOptions { get; private set; } = null!;

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var eventEntity = await _dbContext.Events
            .AsNoTracking()
            .Include(@event => @event.Interests)
            .SingleOrDefaultAsync(@event => @event.Id == id);

        if (eventEntity is null)
        {
            return NotFound();
        }

        if (!CanManage(eventEntity))
        {
            return Forbid();
        }

        Input = new EventListingInputModel
        {
            Title = eventEntity.Title,
            Description = eventEntity.Description ?? string.Empty,
            EventCategoryId = eventEntity.EventCategoryId,
            StartsAt = eventEntity.StartsAt.LocalDateTime,
            EndsAt = eventEntity.EndsAt.LocalDateTime,
            VenueName = eventEntity.VenueName,
            City = eventEntity.City,
            PostalCode = eventEntity.PostalCode,
            ExternalWebsiteUrl = eventEntity.ExternalWebsiteUrl,
            InterestIds = eventEntity.Interests.Select(interest => interest.InterestCategoryId).ToList()
        };

        await LoadAsync(eventEntity);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var eventEntity = await _dbContext.Events
            .Include(@event => @event.Interests)
            .SingleOrDefaultAsync(@event => @event.Id == id);

        if (eventEntity is null)
        {
            return NotFound();
        }

        if (!CanManage(eventEntity))
        {
            return Forbid();
        }

        await LoadAsync(eventEntity);

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var referenceProblem = await _applicationService.CheckListingReferencesAsync(Input, HttpContext.RequestAborted);
        if (referenceProblem is not null)
        {
            ModelState.AddModelError(string.Empty, referenceProblem);
            return Page();
        }

        EventImage? newImage = null;
        if (Input.BackgroundImage is not null)
        {
            var userId = _userManager.GetUserId(User)!;
            (newImage, var imageError) = await _imageService.ReadUploadAsync(Input.BackgroundImage, userId, HttpContext.RequestAborted);
            if (imageError is not null)
            {
                ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.BackgroundImage)}", imageError);
                return Page();
            }
        }

        eventEntity.Title = Input.Title.Trim();
        eventEntity.Description = Input.Description.Trim();
        eventEntity.EventCategoryId = Input.EventCategoryId!.Value;
        eventEntity.StartsAt = Input.StartsAtOffset;
        eventEntity.EndsAt = Input.EndsAtOffset;
        eventEntity.VenueName = Input.VenueName.Trim();
        eventEntity.City = Input.City.Trim();
        eventEntity.PostalCode = string.IsNullOrWhiteSpace(Input.PostalCode) ? null : Input.PostalCode.Trim();
        eventEntity.ExternalWebsiteUrl = string.IsNullOrWhiteSpace(Input.ExternalWebsiteUrl) ? null : Input.ExternalWebsiteUrl.Trim();

        // Replace the interest links with the submitted set.
        var selected = Input.InterestIds.Distinct().ToHashSet();
        foreach (var removed in eventEntity.Interests.Where(interest => !selected.Contains(interest.InterestCategoryId)).ToList())
        {
            eventEntity.Interests.Remove(removed);
        }
        foreach (var addedId in selected.Except(eventEntity.Interests.Select(interest => interest.InterestCategoryId)))
        {
            eventEntity.Interests.Add(new EventInterest { InterestCategoryId = addedId });
        }

        var previousImageId = eventEntity.BackgroundImageId;
        if (newImage is not null)
        {
            eventEntity.BackgroundImage = newImage;
        }
        else if (RemoveBackgroundImage)
        {
            eventEntity.BackgroundImageId = null;
        }

        await _dbContext.SaveChangesAsync(HttpContext.RequestAborted);

        // Clean up the old image if nothing else (such as the original application) still uses it.
        if (previousImageId is int oldImageId && eventEntity.BackgroundImageId != oldImageId)
        {
            await _imageService.DeleteIfUnusedAsync(oldImageId, HttpContext.RequestAborted);
        }

        StatusMessage = "Listing saved.";
        return RedirectToPage(new { id });
    }

    private async Task LoadAsync(Event eventEntity)
    {
        EventId = eventEntity.Id;
        Status = eventEntity.Status;
        ListingOptions = await _applicationService.GetListingFormOptionsAsync(
            eventEntity.BackgroundImageId,
            HttpContext.RequestAborted);
    }

    private bool CanManage(Event eventEntity) =>
        User.IsInRole("Admin") || eventEntity.OrganizerId == _userManager.GetUserId(User);
}

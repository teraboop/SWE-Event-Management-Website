using EventManagement.Data;
using EventManagement.Models.Events;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Controllers;

/// <summary>Serves event images stored in the database.</summary>
public class ImagesController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public ImagesController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    /// <summary>
    /// GET /images/event/5. Anyone may see an image used by a published event.
    /// Images on unpublished events or pending applications are only shown to
    /// the event's organizer, the applicant, and admins.
    /// An image's bytes never change (a replacement gets a new id), so
    /// responses can be cached for a long time.
    /// </summary>
    [HttpGet("/images/event/{id:int}")]
    public async Task<IActionResult> Event(int id)
    {
        var isPublic = await _dbContext.Events.AnyAsync(@event =>
            @event.BackgroundImageId == id && @event.Status == EventStatus.Published);

        if (!isPublic && !await CanSeePrivateImageAsync(id))
        {
            return NotFound();
        }

        var image = await _dbContext.EventImages
            .AsNoTracking()
            .Where(image => image.Id == id)
            .Select(image => new { image.ContentType, image.Data })
            .SingleOrDefaultAsync(HttpContext.RequestAborted);

        if (image is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = isPublic
            ? "public, max-age=31536000, immutable"
            : "private, max-age=3600";
        Response.Headers.XContentTypeOptions = "nosniff";

        return File(image.Data, image.ContentType);
    }

    private async Task<bool> CanSeePrivateImageAsync(int imageId)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return false;
        }

        if (User.IsInRole("Admin"))
        {
            return true;
        }

        return await _dbContext.Events.AnyAsync(@event =>
                   @event.BackgroundImageId == imageId && @event.OrganizerId == userId) ||
               await _dbContext.EventApplications.AnyAsync(application =>
                   application.BackgroundImageId == imageId && application.ApplicantUserId == userId);
    }
}

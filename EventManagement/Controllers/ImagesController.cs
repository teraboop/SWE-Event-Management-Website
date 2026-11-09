using EventManagement.Data;
using EventManagement.Models.Events;
using EventManagement.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Controllers;

/// <summary>Serves uploaded images (event backgrounds and product photos) stored in the database.</summary>
public class ImagesController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly VendorProductService _productService;

    public ImagesController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        VendorProductService productService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _productService = productService;
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

        return await ServeAsync(id, isPublic);
    }

    /// <summary>
    /// GET /images/product/5. Anyone may see the photo of a product in the
    /// shop. Photos of hidden products are only shown to the vendor and admins.
    /// </summary>
    [HttpGet("/images/product/{id:int}")]
    public async Task<IActionResult> Product(int id)
    {
        var isPublic = await _productService.AvailableProducts().AnyAsync(product => product.ImageId == id);

        if (!isPublic && !await CanSeePrivateProductImageAsync(id))
        {
            return NotFound();
        }

        return await ServeAsync(id, isPublic);
    }

    private async Task<IActionResult> ServeAsync(int id, bool isPublic)
    {
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

    private async Task<bool> CanSeePrivateProductImageAsync(int imageId)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return false;
        }

        return User.IsInRole("Admin") ||
               await _dbContext.VendorProducts.AnyAsync(product =>
                   product.ImageId == imageId && product.VendorProfile.OwnerUserId == userId);
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

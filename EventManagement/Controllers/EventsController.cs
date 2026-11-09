using EventManagement.Data;
using EventManagement.Models.Events;
using EventManagement.Services;
using EventManagement.ViewModels.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Controllers;

public class EventsController : Controller
{
    private readonly EventDiscoveryService _eventDiscoveryService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly TicketPurchaseService _ticketPurchaseService;
    private readonly VendorApplicationService _vendorApplicationService;
    private readonly PanelApplicationService _panelApplicationService;
    private readonly EventReviewService _eventReviewService;
    private readonly VendorProductService _vendorProductService;

    public EventsController(
        EventDiscoveryService eventDiscoveryService,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext,
        TicketPurchaseService ticketPurchaseService,
        VendorApplicationService vendorApplicationService,
        PanelApplicationService panelApplicationService,
        EventReviewService eventReviewService,
        VendorProductService vendorProductService)
    {
        _vendorProductService = vendorProductService;
        _panelApplicationService = panelApplicationService;
        _eventReviewService = eventReviewService;
        _eventDiscoveryService = eventDiscoveryService;
        _userManager = userManager;
        _dbContext = dbContext;
        _ticketPurchaseService = ticketPurchaseService;
        _vendorApplicationService = vendorApplicationService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(EventFilterViewModel filter)
    {
        var userId = _userManager.GetUserId(User);

        var model = await _eventDiscoveryService.GetEventsAsync(
            filter,
            userId);

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var UserId = _userManager.GetUserId(User);
        var model = await _eventDiscoveryService.GetEventDetailsAsync(id, UserId);

        if (model is null)
        {
            return NotFound();
        }

        model.ReturnUrl = Url.Action(nameof(Details), new { id }) ?? "/Events";

        model.OrganizerRating = await _eventReviewService.GetOrganizerRatingAsync(id);
        model.ApprovedVendors = await _vendorProductService.GetEventVendorsAsync(id);

        if (model.HasEnded)
        {
            model.Reviews = await _eventReviewService.GetEventReviewsAsync(id, UserId);
            model.Reviews.TicketSalesMode = model.TicketSalesMode;
            model.Reviews.ViewerIsAdmin = User.IsInRole("Admin");
            model.Reviews.ReturnUrl = model.ReturnUrl;
            model.EventRating = model.Reviews.Summary;
        }

        if (UserId is not null)
        {
            model.TicketsHeld = await _ticketPurchaseService.CountTicketsHeldAsync(UserId, id);

            if (model.AcceptsVendorApplications)
            {
                var vendorState = await _vendorApplicationService.GetEventApplicationStateAsync(UserId, id);
                model.HasVendorProfile = vendorState.HasProfile;
                model.VendorApplicationStatus = vendorState.LatestStatus;
            }

            if (model.AcceptsPanelApplications)
            {
                model.PendingPanelProposals = await _panelApplicationService.CountPendingAsync(UserId, id);
            }
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Summary(int id, string? returnUrl = null)
    {
        var UserId = _userManager.GetUserId(User);
        var model = await _eventDiscoveryService.GetEventDetailsAsync(id, UserId);

        if (model is null)
        {
            return NotFound();
        }

        model.ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action(nameof(Index)) ?? "/Events";

        // The quick view shows the event's own rating once it's over, and the
        // organizer's track record before that.
        if (model.HasEnded)
        {
            model.EventRating = await _eventReviewService.GetEventRatingAsync(id);
        }
        else
        {
            model.OrganizerRating = await _eventReviewService.GetOrganizerRatingAsync(id);
        }

        return PartialView("_EventSummaryModal", model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int id, string? returnUrl)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        var isPublished = await _dbContext.Events
            .AnyAsync(@event =>
                @event.Id == id &&
                @event.Status == EventStatus.Published);

        if (!isPublished)
        {
            return NotFound();
        }

        var alreadySaved = await _dbContext.SavedEvents
            .AnyAsync(savedEvent =>
                savedEvent.UserId == userId &&
                savedEvent.EventId == id);

        if (!alreadySaved)
        {
            _dbContext.SavedEvents.Add(new SavedEvent
            {
                UserId = userId,
                EventId = id
            });

            await _dbContext.SaveChangesAsync();
        }

        return RedirectToLocal(returnUrl);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unsave(int id, string? returnUrl)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        var savedEvent = await _dbContext.SavedEvents
            .SingleOrDefaultAsync(item =>
                item.UserId == userId &&
                item.EventId == id);

        if (savedEvent is not null)
        {
            _dbContext.SavedEvents.Remove(savedEvent);
            await _dbContext.SaveChangesAsync();
        }

        return RedirectToLocal(returnUrl);
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }
}
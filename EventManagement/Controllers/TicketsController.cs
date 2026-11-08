using EventManagement.Data;
using EventManagement.Models.Commerce;
using EventManagement.Services;
using EventManagement.ViewModels.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Controllers;

[Authorize]
public class TicketsController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TicketPurchaseService _ticketPurchaseService;
    private readonly TimeProvider _timeProvider;

    public TicketsController(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        TicketPurchaseService ticketPurchaseService,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _ticketPurchaseService = ticketPurchaseService;
        _timeProvider = timeProvider;
    }

    /// <summary>My Tickets: every ticket from the user's completed orders, grouped by event.</summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        var tickets = await _dbContext.Tickets
            .AsNoTracking()
            .Where(ticket =>
                ticket.Order.UserId == userId &&
                ticket.Order.Status == OrderStatus.Completed)
            .Select(ticket => new
            {
                ticket.EventTicketType.Event.Id,
                ticket.EventTicketType.Event.Title,
                ticket.EventTicketType.Event.StartsAt,
                ticket.EventTicketType.Event.EndsAt,
                ticket.EventTicketType.Event.VenueName,
                ticket.EventTicketType.Event.City,
                ticket.EventTicketType.Event.Status,
                Ticket = new MyTicketViewModel
                {
                    OrderId = ticket.OrderId,
                    TicketTypeName = ticket.EventTicketType.Name,
                    UnitPrice = ticket.UnitPrice,
                    Code = ticket.Code,
                    PurchasedAt = ticket.Order.CompletedAt ?? ticket.Order.CreatedAt
                }
            })
            .ToListAsync();

        var now = _timeProvider.GetUtcNow();

        var events = tickets
            .GroupBy(row => row.Id)
            .Select(group =>
            {
                var first = group.First();
                return new MyTicketsEventViewModel
                {
                    EventId = first.Id,
                    Title = first.Title,
                    StartsAt = first.StartsAt,
                    EndsAt = first.EndsAt,
                    VenueName = first.VenueName,
                    City = first.City,
                    Status = first.Status,
                    Tickets = group
                        .Select(row => row.Ticket)
                        .OrderBy(ticket => ticket.OrderId)
                        .ThenBy(ticket => ticket.TicketTypeName)
                        .ToList()
                };
            })
            .ToList();

        // Split on when the event ends, so an event in progress counts as upcoming.
        var model = new MyTicketsViewModel
        {
            UpcomingEvents = events
                .Where(@event => @event.EndsAt >= now)
                .OrderBy(@event => @event.StartsAt)
                .ToList(),
            PastEvents = events
                .Where(@event => @event.EndsAt < now)
                .OrderByDescending(@event => @event.StartsAt)
                .ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Purchase(int eventId, int ticketTypeId, int quantity)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        var result = await _ticketPurchaseService.PurchaseAsync(
            userId, eventId, ticketTypeId, quantity, HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            TempData["TicketError"] = result.Message;
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        TempData["StatusMessage"] = quantity == 1
            ? $"Order #{result.OrderId} confirmed. Your ticket is below."
            : $"Order #{result.OrderId} confirmed. Your {quantity} tickets are below.";

        return RedirectToAction(nameof(Index));
    }
}

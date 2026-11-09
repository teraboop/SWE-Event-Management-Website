using EventManagement.Data;
using EventManagement.ViewModels.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Controllers;

/// <summary>My orders: every order the user has placed, and a receipt for each.</summary>
[Authorize]
public class OrdersController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public OrdersController(ApplicationDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);

        var orders = await _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.UserId == userId)
            .OrderByDescending(order => order.CreatedAt)
            .Select(order => new OrderSummaryViewModel
            {
                Id = order.Id,
                Status = order.Status,
                PlacedAt = order.CompletedAt ?? order.CreatedAt,
                TotalAmount = order.TotalAmount,
                TicketCount = order.Tickets.Count,
                ItemCount = order.Items.Sum(item => (int?)item.Quantity) ?? 0
            })
            .ToListAsync(HttpContext.RequestAborted);

        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = _userManager.GetUserId(User);

        // Another user's order id looks the same as one that doesn't exist.
        var order = await _dbContext.Orders
            .AsNoTracking()
            .Where(item => item.Id == id && item.UserId == userId)
            .Select(item => new OrderDetailsViewModel
            {
                Id = item.Id,
                Status = item.Status,
                PlacedAt = item.CompletedAt ?? item.CreatedAt,
                TotalAmount = item.TotalAmount,
                ItemLines = item.Items
                    .OrderBy(line => line.Id)
                    .Select(line => new OrderItemLineViewModel
                    {
                        ProductId = line.VendorProductId,
                        ProductName = line.ProductName,
                        VendorName = line.VendorName,
                        UnitPrice = line.UnitPrice,
                        Quantity = line.Quantity
                    })
                    .ToList()
            })
            .SingleOrDefaultAsync(HttpContext.RequestAborted);

        if (order is null)
        {
            return NotFound();
        }

        // Tickets are one row each, so collapse them into a line per ticket type and price.
        var tickets = await _dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.OrderId == id)
            .Select(ticket => new
            {
                ticket.EventTicketType.EventId,
                EventTitle = ticket.EventTicketType.Event.Title,
                EventStartsAt = ticket.EventTicketType.Event.StartsAt,
                TicketTypeName = ticket.EventTicketType.Name,
                ticket.UnitPrice
            })
            .ToListAsync(HttpContext.RequestAborted);

        order.TicketLines = tickets
            .GroupBy(ticket => ticket)
            .Select(group => new OrderTicketLineViewModel
            {
                EventId = group.Key.EventId,
                EventTitle = group.Key.EventTitle,
                EventStartsAt = group.Key.EventStartsAt,
                TicketTypeName = group.Key.TicketTypeName,
                UnitPrice = group.Key.UnitPrice,
                Quantity = group.Count()
            })
            .OrderBy(line => line.EventStartsAt)
            .ThenBy(line => line.TicketTypeName)
            .ToList();

        return View(order);
    }
}

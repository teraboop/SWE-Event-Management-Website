using EventManagement.Data;
using EventManagement.Models.Events;
using EventManagement.ViewModels.Organizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Areas.Organizer.Pages.Events;

[Authorize(Roles = "Organizer,Admin")]
public class TicketingModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public TicketingModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    [BindProperty]
    public TicketingSettingsInputModel Input { get; set; } = new();

    public string EventTitle { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var eventEntity = await _dbContext.Events
            .AsNoTracking()
            .Include(@event => @event.TicketTypes)
            .SingleOrDefaultAsync(@event => @event.Id == id);

        if (eventEntity is null)
        {
            return NotFound();
        }

        if (!CanManage(eventEntity))
        {
            return Forbid();
        }

        EventTitle = eventEntity.Title;

        Input = new TicketingSettingsInputModel
        {
            TicketSalesMode = eventEntity.TicketSalesMode,
            ExternalTicketingUrl = eventEntity.ExternalTicketingUrl,
            TicketTypes = eventEntity.TicketTypes
                .OrderBy(ticketType => ticketType.Name)
                .Select(ticketType => new TicketTypeInputModel
                {
                    Id = ticketType.Id,
                    Name = ticketType.Name,
                    Price = ticketType.Price,
                    QuantityAvailable = ticketType.QuantityAvailable,
                    SalesStartAt = ticketType.SalesStartAt,
                    SalesEndAt = ticketType.SalesEndAt,
                    IsEnabled = ticketType.IsEnabled
                })
                .ToList()
        };

        return Page();
    }

    private bool CanManage(Event eventEntity)
    {
        var userId = _userManager.GetUserId(User);

        return User.IsInRole("Admin") ||
            eventEntity.OrganizerId == userId;
    }

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var eventEntity = await _dbContext.Events
            .SingleOrDefaultAsync(@event => @event.Id == id);

        if (eventEntity is null)
        {
            return NotFound();
        }

        if (!CanManage(eventEntity))
        {
            return Forbid();
        }

        EventTitle = eventEntity.Title;

        if (!Enum.IsDefined(Input.TicketSalesMode))
        {
            ModelState.AddModelError(
                nameof(Input.TicketSalesMode),
                "Select a valid ticket sales option.");
        }

        if (Input.TicketSalesMode == TicketSalesMode.External)
        {
            if (string.IsNullOrWhiteSpace(Input.ExternalTicketingUrl))
            {
                ModelState.AddModelError(
                    nameof(Input.ExternalTicketingUrl),
                    "Enter the external ticketing link.");
            }
            else if (!Uri.TryCreate(
                         Input.ExternalTicketingUrl,
                         UriKind.Absolute,
                         out var ticketingUri)
                     || (ticketingUri.Scheme != Uri.UriSchemeHttps
                         && ticketingUri.Scheme != Uri.UriSchemeHttp))
            {
                ModelState.AddModelError(
                    nameof(Input.ExternalTicketingUrl),
                    "Enter a valid link beginning with http:// or https://.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        eventEntity.TicketSalesMode = Input.TicketSalesMode;
        eventEntity.ExternalTicketingUrl =
            Input.TicketSalesMode == TicketSalesMode.External
                ? Input.ExternalTicketingUrl!.Trim()
                : null;

        await _dbContext.SaveChangesAsync();

        StatusMessage = "Ticket settings saved.";
        return RedirectToPage(new { id });
    }
}
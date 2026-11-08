using EventManagement.Data;
using EventManagement.Models.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Areas.Organizer.Pages.Events;

/// <summary>
/// Entry point to the organizer tools: organizers see their own events,
/// admins see every event. Each row links to the ticket settings page.
/// </summary>
[Authorize(Roles = "Organizer,Admin")]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;

    public IndexModel(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager)
    {
        _dbContext = dbContext;
        _userManager = userManager;
    }

    public IReadOnlyList<OrganizerEventRow> Events { get; private set; } = [];

    public bool IsAdmin { get; private set; }

    public async Task OnGetAsync()
    {
        IsAdmin = User.IsInRole("Admin");
        var userId = _userManager.GetUserId(User);

        var query = _dbContext.Events.AsNoTracking();
        if (!IsAdmin)
        {
            query = query.Where(@event => @event.OrganizerId == userId);
        }

        Events = await query
            .OrderByDescending(@event => @event.StartsAt)
            .Select(@event => new OrganizerEventRow
            {
                Id = @event.Id,
                Title = @event.Title,
                StartsAt = @event.StartsAt,
                Status = @event.Status,
                TicketSalesMode = @event.TicketSalesMode,
                TicketsSold = @event.TicketTypes.Sum(ticketType => ticketType.QuantitySold),
                TicketCapacity = @event.TicketTypes.Sum(ticketType => ticketType.QuantityAvailable),
                OrganizerName = @event.Organizer.UserName ?? string.Empty
            })
            .ToListAsync();
    }

    public class OrganizerEventRow
    {
        public int Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public DateTimeOffset StartsAt { get; set; }

        public EventStatus Status { get; set; }

        public TicketSalesMode TicketSalesMode { get; set; }

        public int TicketsSold { get; set; }

        public int TicketCapacity { get; set; }

        public string OrganizerName { get; set; } = string.Empty;
    }
}

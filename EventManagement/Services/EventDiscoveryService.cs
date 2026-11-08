using EventManagement.Data;
using EventManagement.Models.Applications;
using EventManagement.Models.Events;
using EventManagement.ViewModels.Events;
using EventManagement.ViewModels.Panels;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Services;

public class EventDiscoveryService
{
    private readonly ApplicationDbContext _context;

    public EventDiscoveryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EventListViewModel> GetEventsAsync(
        EventFilterViewModel filter,
        string? userId,
        int? limit = null)
    {
        var now = DateTimeOffset.UtcNow;

        IQueryable<Event> query = _context.Events
            .AsNoTracking()
            .Where(@event =>
                @event.Status == EventStatus.Published &&
                @event.StartsAt >= now);

        if (filter.InterestCategoryId.HasValue)
        {
            query = query.Where(@event =>
                @event.Interests.Any(interest =>
                    interest.InterestCategoryId == filter.InterestCategoryId.Value));
        }

        if (filter.StartsAfter.HasValue)
        {
            query = query.Where(@event =>
                @event.StartsAt >= filter.StartsAfter.Value);
        }

        if (filter.StartsBefore.HasValue)
        {
            query = query.Where(@event =>
                @event.StartsAt <= filter.StartsBefore.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            var city = filter.City.Trim();

            query = query.Where(@event => @event.City == city);
        }

        if (filter.MinimumPrice.HasValue)
        {
            query = query.Where(@event =>
                @event.MinimumPrice >= filter.MinimumPrice.Value);
        }

        if (filter.MaximumPrice.HasValue)
        {
            query = query.Where(@event =>
                @event.MinimumPrice <= filter.MaximumPrice.Value);
        }

        if (filter.OpenToVendors)
        {
            query = query.Where(@event => @event.AcceptsVendorApplications);
        }

        if (filter.OpenToPanels)
        {
            query = query.Where(@event => @event.AcceptsPanelApplications);
        }

        var userInterestIds = new List<int>();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            userInterestIds = await _context.UserInterests
                .Where(userInterest => userInterest.UserId == userId)
                .Select(userInterest => userInterest.InterestCategoryId)
                .ToListAsync();
        }

        if (userInterestIds.Count > 0)
        {
            query = query
                .OrderByDescending(@event =>
                    @event.Interests.Any(interest =>
                        userInterestIds.Contains(interest.InterestCategoryId)))
                .ThenByDescending(@event => @event.SavedByUsers.Count)
                .ThenBy(@event => @event.StartsAt);
        }
        else
        {
            query = query
                .OrderByDescending(@event => @event.SavedByUsers.Count)
                .ThenBy(@event => @event.StartsAt);
        }

        if (limit.HasValue)
        {
            query = query.Take(limit.Value);
        }

        var events = await query
            .Select(@event => new EventCardViewModel
            {
                Id = @event.Id,
                Title = @event.Title,
                Description = @event.Description,
                StartsAt = @event.StartsAt,
                VenueName = @event.VenueName,
                City = @event.City,
                MinimumPrice = @event.MinimumPrice,
                ExternalWebsiteUrl = @event.ExternalWebsiteUrl,
                Interests = @event.Interests
                    .OrderBy(interest => interest.InterestCategory.Name)
                    .Select(interest => interest.InterestCategory.Name)
                    .ToList(),
                IsSaved = userId != null &&
                    @event.SavedByUsers.Any(savedEvent => savedEvent.UserId == userId),
                AcceptsVendorApplications = @event.AcceptsVendorApplications,
                AcceptsPanelApplications = @event.AcceptsPanelApplications
            })
            .ToListAsync();

        var interestCategories = await _context.InterestCategories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .ToListAsync();

        return new EventListViewModel
        {
            Filter = filter,
            InterestCategories = interestCategories,
            Events = events
        };
    }
    public async Task<EventDetailsViewModel?> GetEventDetailsAsync(int id, string? userId = null)
    {
        var now = DateTimeOffset.UtcNow;

        return await _context.Events
            .AsNoTracking()
            .Where(@event =>
                @event.Id == id &&
                @event.Status == EventStatus.Published)
            .Select(@event => new EventDetailsViewModel
            {
                Id = @event.Id,
                Title = @event.Title,
                Description = @event.Description,
                StartsAt = @event.StartsAt,
                EndsAt = @event.EndsAt,
                VenueName = @event.VenueName,
                City = @event.City,
                PostalCode = @event.PostalCode,
                MinimumPrice = @event.MinimumPrice,
                TicketSalesMode = @event.TicketSalesMode,
                ExternalTicketingUrl = @event.ExternalTicketingUrl,
                AvailableTicketTypes = @event.TicketTypes
                    .Where(ticketType =>
                        ticketType.IsEnabled &&
                        ticketType.SalesStartAt <= now &&
                        ticketType.SalesEndAt > now)
                    .OrderBy(ticketType => ticketType.Price)
                    .ThenBy(ticketType => ticketType.Name)
                    .Select(ticketType => new EventTicketTypeDetailsViewModel
                    {
                        Id = ticketType.Id,
                        Name = ticketType.Name,
                        Price = ticketType.Price,
                        Remaining = ticketType.QuantityAvailable - ticketType.QuantitySold,
                        SalesStartAt = ticketType.SalesStartAt,
                        SalesEndAt = ticketType.SalesEndAt
                    })
                    .ToList(),
                HasStarted = @event.StartsAt <= now,
                HasEnded = @event.EndsAt <= now,
                AcceptsVendorApplications = @event.AcceptsVendorApplications,
                AcceptsPanelApplications = @event.AcceptsPanelApplications,
                ApprovedPanels = @event.PanelApplications
                    .Where(panel => panel.Status == ApplicationStatus.Approved)
                    .OrderBy(panel => panel.Title)
                    .Select(panel => new EventPanelViewModel
                    {
                        Title = panel.Title,
                        Description = panel.Description,
                        Panelists = panel.Panelists,
                        DurationMinutes = panel.DurationMinutes
                    })
                    .ToList(),
                IsOwnEvent = userId != null && @event.OrganizerId == userId,
                ExternalWebsiteUrl = @event.ExternalWebsiteUrl,
                EventCategoryName = @event.EventCategory.Name,
                OrganizerName = @event.Organizer.UserName ?? "Event organizer",
                Interests = @event.Interests
                    .OrderBy(interest => interest.InterestCategory.Name)
                    .Select(interest => interest.InterestCategory.Name)
                    .ToList(),
                IsSaved = userId != null &&
                    @event.SavedByUsers.Any(savedEvent => savedEvent.UserId == userId),
            })
            .SingleOrDefaultAsync();
    }
}

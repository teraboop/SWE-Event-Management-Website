using EventManagement.Data;
using EventManagement.Models.Events;
using EventManagement.ViewModels.Events;
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
                    .ToList()
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
}

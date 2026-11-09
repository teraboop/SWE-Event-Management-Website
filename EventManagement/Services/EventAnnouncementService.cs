using EventManagement.Data;
using EventManagement.Models.Events;
using EventManagement.ViewModels.Events;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Services;

public sealed record EventAnnouncementResult(bool Succeeded, string Message);

/// <summary>
/// The single owner of the announcement rules: only the event's organizer
/// (or an admin) may post, edit, pin or delete, and readers see pinned
/// announcements first, then the newest.
/// </summary>
public class EventAnnouncementService
{
    /// <summary>Announcements this recent get a "New" badge.</summary>
    public static readonly TimeSpan NewFor = TimeSpan.FromDays(3);

    /// <summary>How many unpinned announcements the event page shows before "Show older updates".</summary>
    public const int RecentShown = 3;

    private readonly ApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public EventAnnouncementService(ApplicationDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <summary>Admins can manage any event's announcements; organizers only their own events'.</summary>
    public Task<bool> CanManageAsync(
        string userId,
        bool userIsAdmin,
        int eventId,
        CancellationToken cancellationToken = default) =>
        _dbContext.Events.AnyAsync(@event =>
            @event.Id == eventId && (userIsAdmin || @event.OrganizerId == userId),
            cancellationToken);

    public async Task<EventAnnouncementResult> PostAsync(
        string userId,
        string userName,
        bool userIsAdmin,
        int eventId,
        EventAnnouncementInputModel input,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(userId, userIsAdmin, eventId, cancellationToken))
        {
            return Fail("Only this event's organizer can post announcements.");
        }

        _dbContext.EventAnnouncements.Add(new EventAnnouncement
        {
            EventId = eventId,
            Title = input.Title.Trim(),
            Body = input.Body.Trim(),
            IsPinned = input.IsPinned,
            PostedByUserId = userId,
            PostedByName = userName,
            CreatedAt = _timeProvider.GetUtcNow()
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        return new EventAnnouncementResult(true, "Announcement posted.");
    }

    public async Task<EventAnnouncementResult> UpdateAsync(
        string userId,
        bool userIsAdmin,
        int eventId,
        int announcementId,
        EventAnnouncementInputModel input,
        CancellationToken cancellationToken = default)
    {
        var updated = await Manageable(userId, userIsAdmin, eventId, announcementId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(announcement => announcement.Title, input.Title.Trim())
                .SetProperty(announcement => announcement.Body, input.Body.Trim())
                .SetProperty(announcement => announcement.IsPinned, input.IsPinned)
                .SetProperty(announcement => announcement.UpdatedAt, _timeProvider.GetUtcNow()),
                cancellationToken);

        return updated == 1
            ? new EventAnnouncementResult(true, "Announcement updated.")
            : NotFound();
    }

    /// <summary>Pinning doesn't count as an edit, so it leaves UpdatedAt alone.</summary>
    public async Task<EventAnnouncementResult> SetPinnedAsync(
        string userId,
        bool userIsAdmin,
        int eventId,
        int announcementId,
        bool pinned,
        CancellationToken cancellationToken = default)
    {
        var updated = await Manageable(userId, userIsAdmin, eventId, announcementId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(announcement => announcement.IsPinned, pinned),
                cancellationToken);

        return updated == 1
            ? new EventAnnouncementResult(true, pinned ? "Announcement pinned." : "Announcement unpinned.")
            : NotFound();
    }

    public async Task<EventAnnouncementResult> DeleteAsync(
        string userId,
        bool userIsAdmin,
        int eventId,
        int announcementId,
        CancellationToken cancellationToken = default)
    {
        var deleted = await Manageable(userId, userIsAdmin, eventId, announcementId)
            .ExecuteDeleteAsync(cancellationToken);

        return deleted == 1
            ? new EventAnnouncementResult(true, "Announcement deleted.")
            : NotFound();
    }

    /// <summary>All of an event's announcements, pinned first, then newest first.</summary>
    public Task<List<EventAnnouncementItem>> GetForEventAsync(
        int eventId,
        CancellationToken cancellationToken = default)
    {
        var newSince = _timeProvider.GetUtcNow() - NewFor;

        return _dbContext.EventAnnouncements
            .AsNoTracking()
            .Where(announcement => announcement.EventId == eventId)
            .OrderByDescending(announcement => announcement.IsPinned)
            .ThenByDescending(announcement => announcement.CreatedAt)
            .ThenByDescending(announcement => announcement.Id)
            .Select(announcement => new EventAnnouncementItem
            {
                Id = announcement.Id,
                Title = announcement.Title,
                Body = announcement.Body,
                IsPinned = announcement.IsPinned,
                PostedByName = announcement.PostedByName,
                CreatedAt = announcement.CreatedAt,
                UpdatedAt = announcement.UpdatedAt,
                IsNew = announcement.CreatedAt >= newSince
            })
            .ToListAsync(cancellationToken);
    }

    /// <summary>The newest announcement for each of these events that has one.</summary>
    public async Task<IReadOnlyDictionary<int, LatestAnnouncement>> GetLatestForEventsAsync(
        IReadOnlyCollection<int> eventIds,
        CancellationToken cancellationToken = default)
    {
        if (eventIds.Count == 0)
        {
            return new Dictionary<int, LatestAnnouncement>();
        }

        var newSince = _timeProvider.GetUtcNow() - NewFor;

        var latest = await _dbContext.Events
            .AsNoTracking()
            .Where(@event => eventIds.Contains(@event.Id))
            .Select(@event => new
            {
                EventId = @event.Id,
                Announcement = @event.Announcements
                    .OrderByDescending(announcement => announcement.CreatedAt)
                    .Select(announcement => new { announcement.Title, announcement.CreatedAt })
                    .FirstOrDefault()
            })
            .Where(row => row.Announcement != null)
            .ToListAsync(cancellationToken);

        return latest.ToDictionary(
            row => row.EventId,
            row => new LatestAnnouncement(
                row.Announcement!.Title,
                row.Announcement.CreatedAt,
                row.Announcement.CreatedAt >= newSince));
    }

    /// <summary>
    /// The announcement, if it belongs to this event and the user may manage
    /// that event. Used as the WHERE of every change, so one event's page
    /// can't be used to change another event's announcements.
    /// </summary>
    private IQueryable<EventAnnouncement> Manageable(string userId, bool userIsAdmin, int eventId, int announcementId) =>
        _dbContext.EventAnnouncements.Where(announcement =>
            announcement.Id == announcementId &&
            announcement.EventId == eventId &&
            (userIsAdmin || announcement.Event.OrganizerId == userId));

    private static EventAnnouncementResult NotFound() =>
        Fail("That announcement doesn't exist, or you can't change it.");

    private static EventAnnouncementResult Fail(string message) => new(false, message);
}

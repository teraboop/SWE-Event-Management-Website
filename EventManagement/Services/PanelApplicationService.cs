using EventManagement.Data;
using EventManagement.Models.Applications;
using EventManagement.Models.Events;
using EventManagement.Models.Panels;
using EventManagement.ViewModels.Panels;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Services;

public sealed record PanelApplicationResult(
    bool Succeeded,
    string Message,
    int? ApplicationId = null);

/// <summary>
/// The single owner of the panel proposal rules, like VendorApplicationService
/// is for vendors: who may submit, who may review, and which status changes
/// are allowed. Pages go through this instead of changing rows themselves.
/// </summary>
public class PanelApplicationService
{
    /// <summary>Pending proposals one user may have for one event at a time.</summary>
    public const int MaxPendingPerEvent = 3;

    public const int MaxReviewNoteLength = 1000;

    /// <summary>Lengths offered on the proposal form, in minutes.</summary>
    public static readonly IReadOnlyList<int> DurationOptions = [30, 45, 60, 90, 120];

    private readonly ApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly EmailNotificationService _notifications;

    public PanelApplicationService(
        ApplicationDbContext dbContext,
        TimeProvider timeProvider,
        EmailNotificationService notifications)
    {
        _notifications = notifications;
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Submits a panel proposal for an event.
    /// <para>Rules:</para>
    /// <list type="bullet">
    /// <item>the event must be published, not over, accepting panel proposals,
    /// and not organized by the applicant;</item>
    /// <item>the applicant may have at most <see cref="MaxPendingPerEvent"/>
    /// pending proposals for the event, none with the same title.</item>
    /// </list>
    /// Field formats are checked by the page's model validation; this method
    /// checks the rules that need the database.
    /// </summary>
    public async Task<PanelApplicationResult> SubmitAsync(
        string userId,
        int eventId,
        PanelApplicationInputModel input,
        CancellationToken cancellationToken = default)
    {
        var eventProblem = await CheckEventAcceptsProposalsAsync(userId, eventId, cancellationToken);
        if (eventProblem is not null)
        {
            return Fail(eventProblem);
        }

        var title = input.Title.Trim();

        var pendingTitles = await _dbContext.PanelApplications
            .Where(application =>
                application.EventId == eventId &&
                application.ApplicantUserId == userId &&
                application.Status == ApplicationStatus.Submitted)
            .Select(application => application.Title)
            .ToListAsync(cancellationToken);

        if (pendingTitles.Count >= MaxPendingPerEvent)
        {
            return Fail($"You already have {MaxPendingPerEvent} proposals waiting for review at this event. Withdraw one or wait for a decision before proposing another.");
        }

        if (pendingTitles.Contains(title, StringComparer.OrdinalIgnoreCase))
        {
            return Fail("You already have a pending proposal with this title for this event.");
        }

        var application = new PanelApplication
        {
            EventId = eventId,
            ApplicantUserId = userId,
            Status = ApplicationStatus.Submitted,
            Title = title,
            Description = input.Description.Trim(),
            Panelists = input.Panelists.Trim(),
            DurationMinutes = input.DurationMinutes,
            ContactEmail = input.ContactEmail.Trim(),
            Message = string.IsNullOrWhiteSpace(input.Message) ? null : input.Message.Trim(),
            SubmittedAt = _timeProvider.GetUtcNow()
        };

        _dbContext.PanelApplications.Add(application);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The pending-title index caught a double submit.
            _dbContext.Entry(application).State = EntityState.Detached;
            return Fail("You already have a pending proposal with this title for this event.");
        }

        await _notifications.PanelApplicationSubmittedAsync(application.Id, cancellationToken);
        return new PanelApplicationResult(true, "Proposal submitted.", application.Id);
    }

    /// <summary>Applicants can withdraw their own proposal while it's still pending.</summary>
    public async Task<PanelApplicationResult> WithdrawAsync(
        string userId,
        int applicationId,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();

        // Conditional UPDATE so a withdrawal and a review at the same moment
        // can't both succeed.
        var updated = await _dbContext.PanelApplications
            .Where(application =>
                application.Id == applicationId &&
                application.ApplicantUserId == userId &&
                application.Status == ApplicationStatus.Submitted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(application => application.Status, ApplicationStatus.Withdrawn)
                .SetProperty(application => application.DecidedAt, now),
                cancellationToken);

        return updated == 1
            ? new PanelApplicationResult(true, "Proposal withdrawn.", applicationId)
            : Fail("This proposal can't be withdrawn because it has already been decided.");
    }

    /// <summary>
    /// Approves or rejects a pending proposal. Only the event's organizer or
    /// an admin may review; rejections need a note so the applicant knows why.
    /// </summary>
    public async Task<PanelApplicationResult> ReviewAsync(
        string reviewerUserId,
        string reviewerName,
        bool reviewerIsAdmin,
        int applicationId,
        bool approve,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var organizerId = await _dbContext.PanelApplications
            .AsNoTracking()
            .Where(application => application.Id == applicationId)
            .Select(application => application.Event.OrganizerId)
            .SingleOrDefaultAsync(cancellationToken);

        if (organizerId is null)
        {
            return Fail("That proposal no longer exists.");
        }

        if (!CanReview(organizerId, reviewerUserId, reviewerIsAdmin))
        {
            return Fail("You don't have permission to review this proposal.");
        }

        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (!approve && note is null)
        {
            return Fail("Add a note explaining why the proposal was rejected.");
        }

        if (note?.Length > MaxReviewNoteLength)
        {
            return Fail($"Keep the note to {MaxReviewNoteLength} characters or fewer.");
        }

        var now = _timeProvider.GetUtcNow();
        var newStatus = approve ? ApplicationStatus.Approved : ApplicationStatus.Rejected;

        var updated = await _dbContext.PanelApplications
            .Where(application =>
                application.Id == applicationId &&
                application.Status == ApplicationStatus.Submitted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(application => application.Status, newStatus)
                .SetProperty(application => application.DecidedAt, now)
                .SetProperty(application => application.ReviewedByUserId, reviewerUserId)
                .SetProperty(application => application.ReviewedByName, reviewerName)
                .SetProperty(application => application.ReviewNote, note),
                cancellationToken);

        if (updated != 1)
        {
            return Fail("This proposal was already decided or withdrawn.");
        }

        await _notifications.PanelApplicationDecidedAsync(applicationId, cancellationToken);
        return new PanelApplicationResult(true, approve ? "Proposal approved." : "Proposal rejected.", applicationId);
    }

    /// <summary>Who may review: admins always; otherwise only the event's organizer.</summary>
    public static bool CanReview(string eventOrganizerId, string reviewerUserId, bool reviewerIsAdmin) =>
        reviewerIsAdmin || eventOrganizerId == reviewerUserId;

    /// <summary>How many of this user's proposals for the event are waiting for review.</summary>
    public Task<int> CountPendingAsync(string userId, int eventId, CancellationToken cancellationToken = default) =>
        _dbContext.PanelApplications.CountAsync(application =>
            application.EventId == eventId &&
            application.ApplicantUserId == userId &&
            application.Status == ApplicationStatus.Submitted,
            cancellationToken);

    /// <summary>
    /// One event's proposals for review. Pending first, oldest first; decided
    /// ones follow, newest first.
    /// </summary>
    public async Task<List<PanelApplicationReviewItem>> GetReviewQueueAsync(
        int eventId,
        CancellationToken cancellationToken = default)
    {
        var items = await _dbContext.PanelApplications
            .AsNoTracking()
            .Where(application => application.EventId == eventId)
            .Select(application => new PanelApplicationReviewItem
            {
                Id = application.Id,
                ApplicantUserName = application.Applicant.UserName ?? string.Empty,
                Title = application.Title,
                Description = application.Description,
                Panelists = application.Panelists,
                DurationMinutes = application.DurationMinutes,
                ContactEmail = application.ContactEmail,
                Message = application.Message,
                Status = application.Status,
                SubmittedAt = application.SubmittedAt,
                DecidedAt = application.DecidedAt,
                ReviewedByName = application.ReviewedByName,
                ReviewNote = application.ReviewNote
            })
            .ToListAsync(cancellationToken);

        var pending = items
            .Where(item => item.Status == ApplicationStatus.Submitted)
            .OrderBy(item => item.SubmittedAt);

        var decided = items
            .Where(item => item.Status != ApplicationStatus.Submitted)
            .OrderByDescending(item => item.DecidedAt);

        return pending.Concat(decided).ToList();
    }

    /// <summary>
    /// Whether this user may propose a panel at this event right now, as a
    /// message to show them, or null when they can.
    /// </summary>
    public async Task<string?> CheckEventAcceptsProposalsAsync(
        string userId,
        int eventId,
        CancellationToken cancellationToken = default)
    {
        var target = await _dbContext.Events
            .AsNoTracking()
            .Where(@event => @event.Id == eventId)
            .Select(@event => new
            {
                @event.Status,
                @event.EndsAt,
                @event.AcceptsPanelApplications,
                @event.OrganizerId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (target is null || target.Status != EventStatus.Published)
        {
            return "That event isn't available.";
        }

        if (target.EndsAt <= _timeProvider.GetUtcNow())
        {
            return "That event has already ended.";
        }

        if (!target.AcceptsPanelApplications)
        {
            return "That event isn't accepting panel proposals.";
        }

        if (target.OrganizerId == userId)
        {
            return "You organize this event, so you can't propose a panel for it.";
        }

        return null;
    }

    /// <summary>Formats a duration for display, e.g. 90 → "1 hr 30 min".</summary>
    public static string FormatDuration(int minutes) =>
        (minutes / 60, minutes % 60) switch
        {
            (0, var m) => $"{m} min",
            (var h, 0) => $"{h} hr",
            (var h, var m) => $"{h} hr {m} min"
        };

    private static PanelApplicationResult Fail(string message) => new(false, message);
}

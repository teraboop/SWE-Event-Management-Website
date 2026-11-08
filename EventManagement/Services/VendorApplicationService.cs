using EventManagement.Data;
using EventManagement.Models.Applications;
using EventManagement.Models.Events;
using EventManagement.Models.Vendors;
using EventManagement.ViewModels.Vendors;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Services;

public sealed record VendorApplicationResult(
    bool Succeeded,
    string Message,
    int? ApplicationId = null);

/// <summary>What the Details page needs to show the vendor section for one user.</summary>
public sealed record VendorEventApplicationState(
    bool HasProfile,
    ApplicationStatus? LatestStatus);

/// <summary>
/// The single owner of the vendor application rules: who may submit, who may
/// review, and which status changes are allowed. Pages call this instead of
/// changing VendorApplication rows themselves, the same way ticket purchases
/// go through TicketPurchaseService.
/// </summary>
public class VendorApplicationService
{
    public const int MaxMessageLength = 1000;
    public const int MaxReviewNoteLength = 1000;

    private readonly ApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public VendorApplicationService(ApplicationDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public Task<VendorProfile?> GetProfileAsync(string userId, CancellationToken cancellationToken = default) =>
        _dbContext.VendorProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.OwnerUserId == userId, cancellationToken);

    /// <summary>
    /// Submits an application from the user's vendor profile: an online-sales
    /// application when <paramref name="eventId"/> is null, otherwise an
    /// application to sell at that event. The business details are copied
    /// from the profile, so the vendor only writes an optional message.
    /// <para>Rules:</para>
    /// <list type="bullet">
    /// <item>the user must have a vendor profile;</item>
    /// <item>they can't already have a pending or approved application for the same thing;</item>
    /// <item>for events: the event must be published, not over, accepting
    /// vendor applications, and not organized by the applicant.</item>
    /// </list>
    /// </summary>
    public async Task<VendorApplicationResult> SubmitAsync(
        string userId,
        int? eventId,
        string? message,
        CancellationToken cancellationToken = default)
    {
        var profile = await _dbContext.VendorProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.OwnerUserId == userId, cancellationToken);

        if (profile is null)
        {
            return Fail("Create a vendor profile before applying.");
        }

        message = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        if (message?.Length > MaxMessageLength)
        {
            return Fail($"Keep your message to {MaxMessageLength} characters or fewer.");
        }

        var kind = eventId is null ? VendorApplicationKind.OnlineSales : VendorApplicationKind.Event;

        if (eventId is int targetEventId)
        {
            var eventProblem = await CheckEventAcceptsApplicationAsync(userId, targetEventId, cancellationToken);
            if (eventProblem is not null)
            {
                return Fail(eventProblem);
            }
        }

        var latestStatus = await LatestStatusAsync(profile.Id, kind, eventId, cancellationToken);
        if (latestStatus == ApplicationStatus.Submitted)
        {
            return Fail("You already have a pending application for this. You can withdraw it from My applications.");
        }

        if (latestStatus == ApplicationStatus.Approved)
        {
            return Fail(kind == VendorApplicationKind.OnlineSales
                ? "You're already approved to sell online."
                : "You're already approved to sell at this event.");
        }

        var application = new VendorApplication
        {
            VendorProfileId = profile.Id,
            Kind = kind,
            EventId = eventId,
            Status = ApplicationStatus.Submitted,
            BusinessName = profile.BusinessName,
            ProductsOffered = profile.ProductsOffered,
            Description = profile.Description,
            ContactEmail = profile.ContactEmail,
            WebsiteUrl = profile.WebsiteUrl,
            Message = message,
            SubmittedAt = _timeProvider.GetUtcNow()
        };

        _dbContext.VendorApplications.Add(application);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The one-pending-application index caught a double submit (for
            // example, the form was submitted twice quickly).
            _dbContext.Entry(application).State = EntityState.Detached;
            return Fail("You already have a pending application for this.");
        }

        return new VendorApplicationResult(true, "Application submitted.", application.Id);
    }

    /// <summary>Applicants can withdraw their own application while it's still pending.</summary>
    public async Task<VendorApplicationResult> WithdrawAsync(
        string userId,
        int applicationId,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();

        // A conditional UPDATE, so a withdrawal and a review happening at the
        // same moment can't both succeed: whichever runs first changes the
        // status, and the other then matches zero rows.
        var updated = await _dbContext.VendorApplications
            .Where(application =>
                application.Id == applicationId &&
                application.VendorProfile.OwnerUserId == userId &&
                application.Status == ApplicationStatus.Submitted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(application => application.Status, ApplicationStatus.Withdrawn)
                .SetProperty(application => application.DecidedAt, now),
                cancellationToken);

        return updated == 1
            ? new VendorApplicationResult(true, "Application withdrawn.", applicationId)
            : Fail("This application can't be withdrawn because it has already been decided.");
    }

    /// <summary>
    /// Approves or rejects a pending application. Online-sales applications
    /// can only be reviewed by admins; event applications by that event's
    /// organizer or an admin. Rejections need a note so the vendor knows why.
    /// </summary>
    public async Task<VendorApplicationResult> ReviewAsync(
        string reviewerUserId,
        string reviewerName,
        bool reviewerIsAdmin,
        int applicationId,
        bool approve,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var target = await _dbContext.VendorApplications
            .AsNoTracking()
            .Where(application => application.Id == applicationId)
            .Select(application => new
            {
                application.Kind,
                application.Status,
                OrganizerId = application.Event != null ? application.Event.OrganizerId : null
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (target is null)
        {
            return Fail("That application no longer exists.");
        }

        if (!CanReview(target.Kind, target.OrganizerId, reviewerUserId, reviewerIsAdmin))
        {
            return Fail("You don't have permission to review this application.");
        }

        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (!approve && note is null)
        {
            return Fail("Add a note explaining why the application was rejected.");
        }

        if (note?.Length > MaxReviewNoteLength)
        {
            return Fail($"Keep the note to {MaxReviewNoteLength} characters or fewer.");
        }

        var now = _timeProvider.GetUtcNow();
        var newStatus = approve ? ApplicationStatus.Approved : ApplicationStatus.Rejected;

        var updated = await _dbContext.VendorApplications
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

        return updated == 1
            ? new VendorApplicationResult(true, approve ? "Application approved." : "Application rejected.", applicationId)
            : Fail("This application was already decided or withdrawn.");
    }

    /// <summary>Who may review: admins always; an event's organizer for that event's applications.</summary>
    public static bool CanReview(
        VendorApplicationKind kind,
        string? eventOrganizerId,
        string reviewerUserId,
        bool reviewerIsAdmin) =>
        reviewerIsAdmin ||
        (kind == VendorApplicationKind.Event && eventOrganizerId == reviewerUserId);

    /// <summary>For #7: may this vendor list merchandise for sale on the website?</summary>
    public Task<bool> IsApprovedForOnlineSalesAsync(int vendorProfileId, CancellationToken cancellationToken = default) =>
        _dbContext.VendorApplications.AnyAsync(application =>
            application.VendorProfileId == vendorProfileId &&
            application.Kind == VendorApplicationKind.OnlineSales &&
            application.Status == ApplicationStatus.Approved,
            cancellationToken);

    /// <summary>For #7: is this vendor approved to sell at this event?</summary>
    public Task<bool> IsApprovedForEventAsync(int vendorProfileId, int eventId, CancellationToken cancellationToken = default) =>
        _dbContext.VendorApplications.AnyAsync(application =>
            application.VendorProfileId == vendorProfileId &&
            application.Kind == VendorApplicationKind.Event &&
            application.EventId == eventId &&
            application.Status == ApplicationStatus.Approved,
            cancellationToken);

    public async Task<VendorEventApplicationState> GetEventApplicationStateAsync(
        string userId,
        int eventId,
        CancellationToken cancellationToken = default)
    {
        var profileId = await _dbContext.VendorProfiles
            .Where(profile => profile.OwnerUserId == userId)
            .Select(profile => (int?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (profileId is null)
        {
            return new VendorEventApplicationState(false, null);
        }

        var latestStatus = await LatestStatusAsync(profileId.Value, VendorApplicationKind.Event, eventId, cancellationToken);
        return new VendorEventApplicationState(true, latestStatus);
    }

    /// <summary>
    /// Status of the vendor's current application for one thing. An approved
    /// application counts over any later one, so a vendor who was approved
    /// always reads as approved.
    /// </summary>
    public Task<ApplicationStatus?> LatestStatusAsync(
        int vendorProfileId,
        VendorApplicationKind kind,
        int? eventId,
        CancellationToken cancellationToken = default) =>
        _dbContext.VendorApplications
            .Where(application =>
                application.VendorProfileId == vendorProfileId &&
                application.Kind == kind &&
                application.EventId == eventId)
            .OrderByDescending(application => application.Status == ApplicationStatus.Approved)
            .ThenByDescending(application => application.SubmittedAt)
            .Select(application => (ApplicationStatus?)application.Status)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Applications for one review queue: online-sales applications when
    /// <paramref name="eventId"/> is null, otherwise that event's applications.
    /// Pending ones come first, oldest first, so reviewers work through them in
    /// order; decided ones follow, newest first.
    /// </summary>
    public async Task<List<VendorApplicationReviewItem>> GetReviewQueueAsync(
        int? eventId,
        CancellationToken cancellationToken = default)
    {
        var kind = eventId is null ? VendorApplicationKind.OnlineSales : VendorApplicationKind.Event;

        var items = await _dbContext.VendorApplications
            .AsNoTracking()
            .Where(application => application.Kind == kind && application.EventId == eventId)
            .Select(application => new VendorApplicationReviewItem
            {
                Id = application.Id,
                ApplicantUserName = application.VendorProfile.Owner.UserName ?? string.Empty,
                BusinessName = application.BusinessName,
                ProductsOffered = application.ProductsOffered,
                Description = application.Description,
                ContactEmail = application.ContactEmail,
                WebsiteUrl = application.WebsiteUrl,
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

    private async Task<string?> CheckEventAcceptsApplicationAsync(
        string userId,
        int eventId,
        CancellationToken cancellationToken)
    {
        var target = await _dbContext.Events
            .AsNoTracking()
            .Where(@event => @event.Id == eventId)
            .Select(@event => new
            {
                @event.Status,
                @event.EndsAt,
                @event.AcceptsVendorApplications,
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

        if (!target.AcceptsVendorApplications)
        {
            return "That event isn't accepting vendor applications.";
        }

        if (target.OrganizerId == userId)
        {
            return "You organize this event, so you can't apply to be a vendor at it.";
        }

        return null;
    }

    private static VendorApplicationResult Fail(string message) => new(false, message);
}

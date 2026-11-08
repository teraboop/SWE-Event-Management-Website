using EventManagement.Data;
using EventManagement.Models.Applications;
using EventManagement.Models.Events;
using EventManagement.ViewModels.Events;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Services;

public sealed record EventApplicationResult(
    bool Succeeded,
    string Message,
    int? ApplicationId = null,
    int? EventId = null);

/// <summary>
/// The single owner of the event application rules: who may submit, how
/// admins decide, and what approval does (create the event, make the
/// applicant its organizer). Pages go through this instead of changing rows
/// themselves.
/// </summary>
public class EventApplicationService
{
    public const string OrganizerRole = "Organizer";

    /// <summary>Pending applications one user may have at a time.</summary>
    public const int MaxPending = 3;

    public const int MaxReviewNoteLength = 1000;

    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly EventImageService _imageService;
    private readonly TimeProvider _timeProvider;

    public EventApplicationService(
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        EventImageService imageService,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _imageService = imageService;
        _timeProvider = timeProvider;
    }

    public Task<int> CountPendingAsync(string userId, CancellationToken cancellationToken = default) =>
        _dbContext.EventApplications.CountAsync(application =>
            application.ApplicantUserId == userId &&
            application.Status == ApplicationStatus.Submitted,
            cancellationToken);

    /// <summary>
    /// Submits an application to list an event.
    /// <para>Rules (field formats are checked by the page's model validation):</para>
    /// <list type="bullet">
    /// <item>the event must start in the future;</item>
    /// <item>the category and interests must exist;</item>
    /// <item>at most <see cref="MaxPending"/> pending applications per user,
    /// none with the same title;</item>
    /// <item>the background image, if any, must be a JPEG, PNG or WebP up to 5 MB.</item>
    /// </list>
    /// </summary>
    public async Task<EventApplicationResult> SubmitAsync(
        string userId,
        EventApplicationInputModel input,
        CancellationToken cancellationToken = default)
    {
        if (input.StartsAtOffset <= _timeProvider.GetUtcNow())
        {
            return Fail("The event's start time must be in the future.");
        }

        var listingProblem = await CheckListingReferencesAsync(input, cancellationToken);
        if (listingProblem is not null)
        {
            return Fail(listingProblem);
        }

        var title = input.Title.Trim();

        var pendingTitles = await _dbContext.EventApplications
            .Where(application =>
                application.ApplicantUserId == userId &&
                application.Status == ApplicationStatus.Submitted)
            .Select(application => application.Title)
            .ToListAsync(cancellationToken);

        if (pendingTitles.Count >= MaxPending)
        {
            return Fail($"You already have {MaxPending} applications waiting for review. Withdraw one or wait for a decision before applying again.");
        }

        if (pendingTitles.Contains(title, StringComparer.OrdinalIgnoreCase))
        {
            return Fail("You already have a pending application for an event with this name.");
        }

        EventImage? image = null;
        if (input.BackgroundImage is not null)
        {
            (image, var imageError) = await _imageService.ReadUploadAsync(input.BackgroundImage, userId, cancellationToken);
            if (imageError is not null)
            {
                return Fail(imageError);
            }
        }

        var application = new EventApplication
        {
            ApplicantUserId = userId,
            Status = ApplicationStatus.Submitted,
            Title = title,
            Description = input.Description.Trim(),
            EventCategoryId = input.EventCategoryId!.Value,
            StartsAt = input.StartsAtOffset,
            EndsAt = input.EndsAtOffset,
            VenueName = input.VenueName.Trim(),
            City = input.City.Trim(),
            PostalCode = NullIfBlank(input.PostalCode),
            ExternalWebsiteUrl = NullIfBlank(input.ExternalWebsiteUrl),
            BackgroundImage = image,
            Interests = input.InterestIds
                .Distinct()
                .Select(interestId => new EventApplicationInterest { InterestCategoryId = interestId })
                .ToList(),
            ContactName = input.ContactName.Trim(),
            ContactEmail = input.ContactEmail.Trim(),
            ContactPhone = NullIfBlank(input.ContactPhone),
            OrganizationName = NullIfBlank(input.OrganizationName),
            Message = NullIfBlank(input.Message),
            SubmittedAt = _timeProvider.GetUtcNow()
        };

        _dbContext.EventApplications.Add(application);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The pending-title index caught a double submit.
            _dbContext.ChangeTracker.Clear();
            return Fail("You already have a pending application for an event with this name.");
        }

        return new EventApplicationResult(true, "Application submitted.", application.Id);
    }

    /// <summary>Applicants can withdraw their own application while it's still pending.</summary>
    public async Task<EventApplicationResult> WithdrawAsync(
        string userId,
        int applicationId,
        CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();

        var updated = await _dbContext.EventApplications
            .Where(application =>
                application.Id == applicationId &&
                application.ApplicantUserId == userId &&
                application.Status == ApplicationStatus.Submitted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(application => application.Status, ApplicationStatus.Withdrawn)
                .SetProperty(application => application.DecidedAt, now),
                cancellationToken);

        return updated == 1
            ? new EventApplicationResult(true, "Application withdrawn.", applicationId)
            : Fail("This application can't be withdrawn because it has already been decided.");
    }

    /// <summary>
    /// Approves or rejects a pending application. Only admins may decide.
    /// Approving, in one transaction:
    /// <list type="number">
    /// <item>creates a published Event from the application, owned by the applicant;</item>
    /// <item>marks the application approved and links it to the event;</item>
    /// <item>gives the applicant the Organizer role if they don't have it.</item>
    /// </list>
    /// If any step fails, none of it happens.
    /// </summary>
    public async Task<EventApplicationResult> ReviewAsync(
        string reviewerUserId,
        string reviewerName,
        bool reviewerIsAdmin,
        int applicationId,
        bool approve,
        string? note,
        CancellationToken cancellationToken = default)
    {
        if (!reviewerIsAdmin)
        {
            return Fail("Only admins can review event applications.");
        }

        note = NullIfBlank(note);
        if (!approve && note is null)
        {
            return Fail("Add a note explaining why the application was rejected.");
        }

        if (note?.Length > MaxReviewNoteLength)
        {
            return Fail($"Keep the note to {MaxReviewNoteLength} characters or fewer.");
        }

        var now = _timeProvider.GetUtcNow();

        if (!approve)
        {
            var rejected = await _dbContext.EventApplications
                .Where(application =>
                    application.Id == applicationId &&
                    application.Status == ApplicationStatus.Submitted)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(application => application.Status, ApplicationStatus.Rejected)
                    .SetProperty(application => application.DecidedAt, now)
                    .SetProperty(application => application.ReviewedByUserId, reviewerUserId)
                    .SetProperty(application => application.ReviewedByName, reviewerName)
                    .SetProperty(application => application.ReviewNote, note),
                    cancellationToken);

            return rejected == 1
                ? new EventApplicationResult(true, "Application rejected.", applicationId)
                : Fail("This application was already decided or withdrawn.");
        }

        var source = await _dbContext.EventApplications
            .AsNoTracking()
            .Include(application => application.Interests)
            .SingleOrDefaultAsync(application =>
                application.Id == applicationId &&
                application.Status == ApplicationStatus.Submitted,
                cancellationToken);

        if (source is null)
        {
            return Fail("This application was already decided or withdrawn.");
        }

        if (source.StartsAt <= now)
        {
            return Fail("This event's start time has already passed, so it can't be listed. Reject it with a note instead.");
        }

        var applicant = await _userManager.FindByIdAsync(source.ApplicantUserId);
        if (applicant is null)
        {
            return Fail("The applicant's account no longer exists.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var createdEvent = new Event
        {
            OrganizerId = source.ApplicantUserId,
            EventCategoryId = source.EventCategoryId,
            Title = source.Title,
            Description = source.Description,
            StartsAt = source.StartsAt,
            EndsAt = source.EndsAt,
            VenueName = source.VenueName,
            City = source.City,
            PostalCode = source.PostalCode,
            ExternalWebsiteUrl = source.ExternalWebsiteUrl,
            BackgroundImageId = source.BackgroundImageId,
            Status = EventStatus.Published,
            CreatedAt = now,
            Interests = source.Interests
                .Select(interest => new EventInterest { InterestCategoryId = interest.InterestCategoryId })
                .ToList()
        };

        _dbContext.Events.Add(createdEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Conditional UPDATE: if another admin decided it, or the applicant
        // withdrew it, since we read it, this matches nothing and the new
        // event is rolled back.
        var approved = await _dbContext.EventApplications
            .Where(application =>
                application.Id == applicationId &&
                application.Status == ApplicationStatus.Submitted)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(application => application.Status, ApplicationStatus.Approved)
                .SetProperty(application => application.DecidedAt, now)
                .SetProperty(application => application.ReviewedByUserId, reviewerUserId)
                .SetProperty(application => application.ReviewedByName, reviewerName)
                .SetProperty(application => application.ReviewNote, note)
                .SetProperty(application => application.CreatedEventId, createdEvent.Id),
                cancellationToken);

        if (approved != 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            _dbContext.ChangeTracker.Clear();
            return Fail("This application was already decided or withdrawn.");
        }

        if (!await _userManager.IsInRoleAsync(applicant, OrganizerRole))
        {
            var roleResult = await _userManager.AddToRoleAsync(applicant, OrganizerRole);
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();
                return Fail("Couldn't give the applicant the Organizer role: " +
                    string.Join(" ", roleResult.Errors.Select(error => error.Description)));
            }
        }

        await transaction.CommitAsync(cancellationToken);

        return new EventApplicationResult(
            true,
            $"Application approved. \"{source.Title}\" is now listed, and {applicant.UserName} is its organizer.",
            applicationId,
            createdEvent.Id);
    }

    /// <summary>
    /// All applications for the admin queue. Pending first, oldest first;
    /// decided ones follow, newest first.
    /// </summary>
    public async Task<List<EventApplicationReviewItem>> GetReviewQueueAsync(CancellationToken cancellationToken = default)
    {
        var items = await _dbContext.EventApplications
            .AsNoTracking()
            .Select(application => new EventApplicationReviewItem
            {
                Id = application.Id,
                ApplicantUserName = application.Applicant.UserName ?? string.Empty,
                Title = application.Title,
                Description = application.Description,
                EventCategoryName = application.EventCategory.Name,
                StartsAt = application.StartsAt,
                EndsAt = application.EndsAt,
                VenueName = application.VenueName,
                City = application.City,
                PostalCode = application.PostalCode,
                ExternalWebsiteUrl = application.ExternalWebsiteUrl,
                BackgroundImageId = application.BackgroundImageId,
                Interests = application.Interests
                    .Select(interest => interest.InterestCategory.Name)
                    .OrderBy(name => name)
                    .ToList(),
                ContactName = application.ContactName,
                ContactEmail = application.ContactEmail,
                ContactPhone = application.ContactPhone,
                OrganizationName = application.OrganizationName,
                Message = application.Message,
                Status = application.Status,
                SubmittedAt = application.SubmittedAt,
                DecidedAt = application.DecidedAt,
                ReviewedByName = application.ReviewedByName,
                ReviewNote = application.ReviewNote,
                CreatedEventId = application.CreatedEventId
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

    /// <summary>Category and interest choices for the listing form.</summary>
    public async Task<EventListingFormOptions> GetListingFormOptionsAsync(
        int? currentImageId = null,
        CancellationToken cancellationToken = default)
    {
        var categories = await _dbContext.EventCategories
            .AsNoTracking()
            .OrderBy(category => category.Name)
            .ToListAsync(cancellationToken);

        var interests = await _dbContext.InterestCategories
            .AsNoTracking()
            .OrderBy(interest => interest.Name)
            .ToListAsync(cancellationToken);

        return new EventListingFormOptions(categories, interests, currentImageId);
    }

    /// <summary>Checks that the chosen category and interests exist. Returns a message, or null when they do.</summary>
    public async Task<string?> CheckListingReferencesAsync(
        EventListingInputModel input,
        CancellationToken cancellationToken = default)
    {
        var categoryExists = await _dbContext.EventCategories
            .AnyAsync(category => category.Id == input.EventCategoryId, cancellationToken);

        if (!categoryExists)
        {
            return "Choose a category from the list.";
        }

        var interestIds = input.InterestIds.Distinct().ToList();
        if (interestIds.Count > 0)
        {
            var knownCount = await _dbContext.InterestCategories
                .CountAsync(interest => interestIds.Contains(interest.Id), cancellationToken);

            if (knownCount != interestIds.Count)
            {
                return "Choose interests from the list.";
            }
        }

        return null;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static EventApplicationResult Fail(string message) => new(false, message);
}

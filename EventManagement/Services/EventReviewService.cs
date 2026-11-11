using EventManagement.Data;
using EventManagement.Models.Events;
using EventManagement.Models.Reviews;
using EventManagement.ViewModels.Reviews;
using Microsoft.EntityFrameworkCore;

namespace EventManagement.Services;

public enum ReviewIneligibilityReason
{
    NotSignedIn,
    EventNotFound,
    EventNotOver,
    OrganizerOwnEvent,
    NoVerifiedTicket
}

public sealed record EventReviewResult(
    bool Succeeded,
    string Message,
    int? EventId = null);

/// <summary>
/// The single owner of the review rules: who may review an event, one review
/// per person per event, and who may delete one. Controllers go through this
/// instead of changing rows themselves.
/// </summary>
public class EventReviewService
{
    public const int MinRating = 1;

    public const int MaxRating = 5;

    public const int MaxCommentLength = 2000;

    /// <summary>How many reviews the event page lists; the summary still counts them all.</summary>
    public const int MaxReviewsShown = 50;

    /// <summary>Shown in place of a name when the author hasn't set a display name.</summary>
    public const string AnonymousAuthorName = "Attendee";

    private readonly ApplicationDbContext _dbContext;
    private readonly TicketPurchaseService _ticketPurchaseService;
    private readonly RewardService _rewardService;
    private readonly TimeProvider _timeProvider;

    public EventReviewService(
        ApplicationDbContext dbContext,
        TicketPurchaseService ticketPurchaseService,
        RewardService rewardService,
        TimeProvider timeProvider)
    {
        _rewardService = rewardService;
        _dbContext = dbContext;
        _ticketPurchaseService = ticketPurchaseService;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Whether a user may write (or edit) a review of an event.
    /// <para>Rules, checked in this order:</para>
    /// <list type="bullet">
    /// <item>the user is signed in;</item>
    /// <item>the event is published and has ended;</item>
    /// <item>the user doesn't organize it;</item>
    /// <item>the user holds a ticket from a completed order, so every review
    /// comes from someone who could actually have attended.</item>
    /// </list>
    /// Returns null when the user may review.
    /// </summary>
    public async Task<ReviewIneligibilityReason?> CheckEligibilityAsync(
        string? userId,
        int eventId,
        CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            return ReviewIneligibilityReason.NotSignedIn;
        }

        var reviewedEvent = await _dbContext.Events
            .AsNoTracking()
            .Where(@event => @event.Id == eventId && @event.Status == EventStatus.Published)
            .Select(@event => new { @event.OrganizerId, @event.EndsAt })
            .SingleOrDefaultAsync(cancellationToken);

        if (reviewedEvent is null)
        {
            return ReviewIneligibilityReason.EventNotFound;
        }

        if (reviewedEvent.EndsAt > _timeProvider.GetUtcNow())
        {
            return ReviewIneligibilityReason.EventNotOver;
        }

        if (reviewedEvent.OrganizerId == userId)
        {
            return ReviewIneligibilityReason.OrganizerOwnEvent;
        }

        if (!await _ticketPurchaseService.HasVerifiedPurchaseAsync(userId, eventId, cancellationToken))
        {
            return ReviewIneligibilityReason.NoVerifiedTicket;
        }

        return null;
    }

    public static string Explain(ReviewIneligibilityReason reason) => reason switch
    {
        ReviewIneligibilityReason.NotSignedIn => "Log in to review this event.",
        ReviewIneligibilityReason.EventNotFound => "This event isn't available for reviews.",
        ReviewIneligibilityReason.EventNotOver => "Reviews open once the event has ended.",
        ReviewIneligibilityReason.OrganizerOwnEvent => "You organize this event, so you can't review it.",
        ReviewIneligibilityReason.NoVerifiedTicket => "Only attendees who got tickets through this site can review this event.",
        _ => "You can't review this event."
    };

    /// <summary>
    /// Creates the user's review of an event, or updates it if they already
    /// wrote one, so resubmitting the form never makes a duplicate.
    /// </summary>
    public async Task<EventReviewResult> SaveAsync(
        string userId,
        int eventId,
        int rating,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        if (rating is < MinRating or > MaxRating)
        {
            return Fail($"Choose a rating from {MinRating} to {MaxRating} stars.", eventId);
        }

        comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        if (comment?.Length > MaxCommentLength)
        {
            return Fail($"Keep your review under {MaxCommentLength} characters.", eventId);
        }

        var problem = await CheckEligibilityAsync(userId, eventId, cancellationToken);
        if (problem is not null)
        {
            return Fail(Explain(problem.Value), eventId);
        }

        var now = _timeProvider.GetUtcNow();

        var existing = await FindOwnReviewAsync(userId, eventId, cancellationToken);
        if (existing is not null)
        {
            ApplyEdit(existing, rating, comment, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return new EventReviewResult(true, "Your review has been updated.", eventId);
        }

        var review = new EventReview
        {
            EventId = eventId,
            AuthorUserId = userId,
            Rating = rating,
            Comment = comment,
            CreatedAt = now
        };

        _dbContext.EventReviews.Add(review);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The one-review-per-event index caught a form submitted twice at
            // once. Apply this submission to the review that won instead.
            _dbContext.Entry(review).State = EntityState.Detached;

            var winner = await FindOwnReviewAsync(userId, eventId, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            ApplyEdit(winner, rating, comment, now);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var eventTitle = await _dbContext.Events
            .Where(@event => @event.Id == eventId)
            .Select(@event => @event.Title)
            .SingleAsync(cancellationToken);

        var pointsEarned = await _rewardService.AwardForReviewAsync(userId, eventId, eventTitle, cancellationToken);

        return new EventReviewResult(
            true,
            pointsEarned > 0
                ? $"Thanks for reviewing this event! You earned {pointsEarned} reward points."
                : "Thanks for reviewing this event.",
            eventId);
    }

    /// <summary>Authors can delete their own review; admins can remove any review.</summary>
    public async Task<EventReviewResult> DeleteAsync(
        string userId,
        bool userIsAdmin,
        int reviewId,
        CancellationToken cancellationToken = default)
    {
        var review = await _dbContext.EventReviews
            .AsNoTracking()
            .Where(item => item.Id == reviewId)
            .Select(item => new { item.EventId, item.AuthorUserId })
            .SingleOrDefaultAsync(cancellationToken);

        if (review is null)
        {
            return Fail("That review has already been deleted.");
        }

        var isOwn = review.AuthorUserId == userId;
        if (!isOwn && !userIsAdmin)
        {
            return Fail("You can only delete your own review.", review.EventId);
        }

        await _dbContext.EventReviews
            .Where(item => item.Id == reviewId)
            .ExecuteDeleteAsync(cancellationToken);

        return new EventReviewResult(
            true,
            isOwn ? "Your review has been deleted." : "The review has been removed.",
            review.EventId);
    }

    /// <summary>The summary, the latest reviews and the viewer's own review and eligibility for one event.</summary>
    public async Task<EventReviewsViewModel> GetEventReviewsAsync(
        int eventId,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var reviews = _dbContext.EventReviews
            .AsNoTracking()
            .Where(review => review.EventId == eventId);

        var summary = await SummarizeAsync(reviews, cancellationToken);

        var items = await reviews
            .OrderByDescending(review => review.CreatedAt)
            .ThenByDescending(review => review.Id)
            .Take(MaxReviewsShown)
            .Select(review => new EventReviewItem
            {
                Id = review.Id,
                Rating = review.Rating,
                Comment = review.Comment,
                AuthorName = review.Author.DisplayName ?? AnonymousAuthorName,
                CreatedAt = review.CreatedAt,
                UpdatedAt = review.UpdatedAt,
                IsOwn = userId != null && review.AuthorUserId == userId
            })
            .ToListAsync(cancellationToken);

        // The user's own review may be older than the ones listed, so fetch it separately.
        var myReview = items.FirstOrDefault(item => item.IsOwn);
        if (myReview is null && userId is not null)
        {
            myReview = await reviews
                .Where(review => review.AuthorUserId == userId)
                .Select(review => new EventReviewItem
                {
                    Id = review.Id,
                    Rating = review.Rating,
                    Comment = review.Comment,
                    AuthorName = review.Author.DisplayName ?? AnonymousAuthorName,
                    CreatedAt = review.CreatedAt,
                    UpdatedAt = review.UpdatedAt,
                    IsOwn = true
                })
                .SingleOrDefaultAsync(cancellationToken);
        }

        return new EventReviewsViewModel
        {
            EventId = eventId,
            Summary = summary,
            Reviews = items,
            MyReview = myReview,
            Ineligibility = await CheckEligibilityAsync(userId, eventId, cancellationToken)
        };
    }

    public Task<RatingSummary> GetEventRatingAsync(
        int eventId,
        CancellationToken cancellationToken = default) =>
        SummarizeAsync(_dbContext.EventReviews.Where(review => review.EventId == eventId), cancellationToken);

    /// <summary>
    /// The rating across every published event run by this event's organizer,
    /// so upcoming events, which can't be reviewed yet, still show how the
    /// organizer's past events went.
    /// </summary>
    public Task<RatingSummary> GetOrganizerRatingAsync(
        int eventId,
        CancellationToken cancellationToken = default)
    {
        var organizerId = _dbContext.Events
            .Where(@event => @event.Id == eventId)
            .Select(@event => @event.OrganizerId);

        return SummarizeAsync(
            _dbContext.EventReviews.Where(review =>
                review.Event.Status == EventStatus.Published &&
                organizerId.Contains(review.Event.OrganizerId)),
            cancellationToken);
    }

    /// <summary>The stars the user gave each of these events, for those they've reviewed.</summary>
    public async Task<IReadOnlyDictionary<int, int>> GetUserRatingsAsync(
        string userId,
        IReadOnlyCollection<int> eventIds,
        CancellationToken cancellationToken = default)
    {
        if (eventIds.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        return await _dbContext.EventReviews
            .AsNoTracking()
            .Where(review => review.AuthorUserId == userId && eventIds.Contains(review.EventId))
            .ToDictionaryAsync(review => review.EventId, review => review.Rating, cancellationToken);
    }

    private Task<EventReview?> FindOwnReviewAsync(string userId, int eventId, CancellationToken cancellationToken) =>
        _dbContext.EventReviews.SingleOrDefaultAsync(
            review => review.EventId == eventId && review.AuthorUserId == userId,
            cancellationToken);

    private static void ApplyEdit(EventReview review, int rating, string? comment, DateTimeOffset now)
    {
        review.Rating = rating;
        review.Comment = comment;
        review.UpdatedAt = now;
    }

    private static async Task<RatingSummary> SummarizeAsync(
        IQueryable<EventReview> reviews,
        CancellationToken cancellationToken)
    {
        var counts = await reviews
            .GroupBy(review => review.Rating)
            .Select(group => new { Rating = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var countsByStars = new int[MaxRating];
        foreach (var row in counts)
        {
            if (row.Rating is >= MinRating and <= MaxRating)
            {
                countsByStars[row.Rating - 1] = row.Count;
            }
        }

        return new RatingSummary(countsByStars);
    }

    private static EventReviewResult Fail(string message, int? eventId = null) =>
        new(false, message, eventId);
}

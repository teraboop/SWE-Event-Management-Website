using EventManagement.Models.Events;
using EventManagement.Services;

namespace EventManagement.ViewModels.Reviews;

/// <summary>Average rating and how many reviews gave each number of stars.</summary>
public sealed class RatingSummary
{
    public static readonly RatingSummary Empty = new([0, 0, 0, 0, 0]);

    /// <param name="countsByStars">Five counts: index 0 is 1-star reviews, index 4 is 5-star.</param>
    public RatingSummary(IReadOnlyList<int> countsByStars)
    {
        CountsByStars = countsByStars;
        Count = countsByStars.Sum();
        Average = Count == 0
            ? 0
            : countsByStars.Select((count, index) => count * (index + 1)).Sum() / (double)Count;
    }

    public IReadOnlyList<int> CountsByStars { get; }

    public int Count { get; }

    public double Average { get; }

    public bool HasReviews => Count > 0;

    public string AverageText => Average.ToString("0.0");

    public string CountText => Count == 1 ? "1 review" : $"{Count} reviews";

    public int CountFor(int stars) => CountsByStars[stars - 1];

    public int PercentFor(int stars) =>
        Count == 0 ? 0 : (int)Math.Round(100.0 * CountFor(stars) / Count);
}

public class EventReviewItem
{
    public int Id { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public string AuthorName { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public bool IsOwn { get; set; }
}

/// <summary>Everything the Reviews section of an event page needs.</summary>
public class EventReviewsViewModel
{
    public int EventId { get; set; }

    public RatingSummary Summary { get; set; } = RatingSummary.Empty;

    /// <summary>Most recent reviews first, capped at <see cref="EventReviewService.MaxReviewsShown"/>.</summary>
    public IReadOnlyList<EventReviewItem> Reviews { get; set; } = [];

    /// <summary>The signed-in user's own review, if they've written one.</summary>
    public EventReviewItem? MyReview { get; set; }

    /// <summary>Why the signed-in user can't review this event, or null if they can.</summary>
    public ReviewIneligibilityReason? Ineligibility { get; set; }

    public bool CanReview => Ineligibility is null;

    public TicketSalesMode TicketSalesMode { get; set; }

    public bool ViewerIsAdmin { get; set; }

    /// <summary>Where the login link should send the user back to.</summary>
    public string ReturnUrl { get; set; } = "/Events";
}

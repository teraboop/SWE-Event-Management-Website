using EventManagement.Data;
using EventManagement.Models.Rewards;
using EventManagement.ViewModels.Rewards;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EventManagement.Services;

/// <summary>
/// The rewards program (#5), and the single owner of its rules:
/// <list type="bullet">
/// <item>earn <see cref="PointsPerDollar"/> point per whole dollar paid on an order;</item>
/// <item>earn <see cref="PointsPerReview"/> points for reviewing an attended event, once per event;</item>
/// <item>spend points in blocks of <see cref="PointsPerRedemption"/> for
/// <see cref="RedemptionValue"/> off an order at checkout;</item>
/// <item>tiers come from lifetime points and are for display only.</item>
/// </list>
/// Every change writes a history row and updates the balance in the same
/// transaction, so the balance always equals the history's total.
/// </summary>
public class RewardService
{
    public const int PointsPerDollar = 1;

    public const int PointsPerReview = 25;

    public const int PointsPerRedemption = 100;

    public const decimal RedemptionValue = 5m;

    public const int HistoryShown = 50;

    public static readonly IReadOnlyList<RewardTier> Tiers =
    [
        new("Bronze", 0),
        new("Silver", 250),
        new("Gold", 1000)
    ];

    private readonly ApplicationDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public RewardService(ApplicationDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    /// <summary>Points earned for paying this much. Cents don't count.</summary>
    public static int PointsForPurchase(decimal amountPaid) =>
        amountPaid <= 0 ? 0 : (int)Math.Floor(amountPaid) * PointsPerDollar;

    public static decimal DiscountFor(int points) => points / PointsPerRedemption * RedemptionValue;

    /// <summary>
    /// The most points usable on an order: whole redemptions only, no more
    /// than the balance, and never more discount than the order costs.
    /// </summary>
    public static int MaxRedeemablePoints(int balance, decimal subtotal)
    {
        var affordable = balance / PointsPerRedemption;
        var useful = (int)Math.Floor(subtotal / RedemptionValue);
        return Math.Max(0, Math.Min(affordable, useful)) * PointsPerRedemption;
    }

    public static RewardTier TierFor(int lifetimePoints) =>
        Tiers.Last(tier => lifetimePoints >= tier.MinimumLifetimePoints);

    public static RewardTier? NextTierAfter(int lifetimePoints) =>
        Tiers.FirstOrDefault(tier => tier.MinimumLifetimePoints > lifetimePoints);

    public async Task<int> GetBalanceAsync(string userId, CancellationToken cancellationToken = default) =>
        await _dbContext.RewardAccounts
            .Where(account => account.UserId == userId)
            .Select(account => (int?)account.Points)
            .SingleOrDefaultAsync(cancellationToken) ?? 0;

    public async Task<RewardsViewModel> GetSummaryAsync(string userId, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.RewardAccounts
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => new { item.Points, item.LifetimePoints })
            .SingleOrDefaultAsync(cancellationToken);

        var points = account?.Points ?? 0;
        var lifetime = account?.LifetimePoints ?? 0;

        return new RewardsViewModel
        {
            Points = points,
            LifetimePoints = lifetime,
            RedeemableValue = DiscountFor(points),
            Tier = TierFor(lifetime),
            NextTier = NextTierAfter(lifetime),
            History = await _dbContext.RewardTransactions
                .AsNoTracking()
                .Where(item => item.UserId == userId)
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.Id)
                .Take(HistoryShown)
                .Select(item => new RewardHistoryItem
                {
                    CreatedAt = item.CreatedAt,
                    Description = item.Description,
                    Points = item.Points,
                    Kind = item.Kind,
                    OrderId = item.OrderId,
                    EventId = item.EventId
                })
                .ToListAsync(cancellationToken)
        };
    }

    /// <summary>
    /// Takes points for a discount on an order. Only CheckoutService calls
    /// this, inside its order transaction. The UPDATE only succeeds while the
    /// balance covers it, so two checkouts at once can't overspend. Returns
    /// false, changing nothing, when there aren't enough points.
    /// </summary>
    public async Task<bool> TrySpendForOrderAsync(
        string userId,
        int points,
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var spent = await _dbContext.RewardAccounts
            .Where(account => account.UserId == userId && account.Points >= points)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(account => account.Points, account => account.Points - points),
                cancellationToken);

        if (spent == 0)
        {
            return false;
        }

        _dbContext.RewardTransactions.Add(new RewardTransaction
        {
            UserId = userId,
            Points = -points,
            Kind = RewardTransactionKind.Redemption,
            Description = $"Used {DiscountFor(points):C} off order #{orderId}",
            OrderId = orderId,
            CreatedAt = _timeProvider.GetUtcNow()
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>Points for a placed order. Called by CheckoutService inside its order transaction.</summary>
    public async Task EarnForOrderAsync(
        string userId,
        int points,
        int orderId,
        CancellationToken cancellationToken = default)
    {
        if (points <= 0)
        {
            return;
        }

        _dbContext.RewardTransactions.Add(new RewardTransaction
        {
            UserId = userId,
            Points = points,
            Kind = RewardTransactionKind.Purchase,
            Description = $"Order #{orderId}",
            OrderId = orderId,
            CreatedAt = _timeProvider.GetUtcNow()
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
        await CreditAsync(userId, points, cancellationToken);
    }

    /// <summary>
    /// Points for reviewing an event, once per event: deleting and rewriting
    /// a review doesn't earn again. Returns the points awarded, or 0.
    /// EventReviewService has already checked the user may review it.
    /// </summary>
    public async Task<int> AwardForReviewAsync(
        string userId,
        int eventId,
        string eventTitle,
        CancellationToken cancellationToken = default)
    {
        if (await _dbContext.RewardTransactions.AnyAsync(item =>
                item.UserId == userId && item.EventId == eventId && item.Kind == RewardTransactionKind.Review,
                cancellationToken))
        {
            return 0;
        }

        await using var transaction = await BeginUnlessInTransactionAsync(cancellationToken);

        var award = new RewardTransaction
        {
            UserId = userId,
            Points = PointsPerReview,
            Kind = RewardTransactionKind.Review,
            Description = $"Reviewed {eventTitle}",
            EventId = eventId,
            CreatedAt = _timeProvider.GetUtcNow()
        };
        _dbContext.RewardTransactions.Add(award);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The once-per-event index caught a review posted twice at once.
            _dbContext.Entry(award).State = EntityState.Detached;
            return 0;
        }

        await CreditAsync(userId, PointsPerReview, cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return PointsPerReview;
    }

    /// <summary>Adds points to the balance, opening the account on first use.</summary>
    private async Task CreditAsync(string userId, int points, CancellationToken cancellationToken)
    {
        if (await AddToExistingAccountAsync(userId, points, cancellationToken))
        {
            return;
        }

        var account = new RewardAccount { UserId = userId, Points = points, LifetimePoints = points };
        _dbContext.RewardAccounts.Add(account);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another request opened the account first; add to that one.
            _dbContext.Entry(account).State = EntityState.Detached;
            if (!await AddToExistingAccountAsync(userId, points, cancellationToken))
            {
                throw;
            }
        }
    }

    private async Task<bool> AddToExistingAccountAsync(string userId, int points, CancellationToken cancellationToken) =>
        await _dbContext.RewardAccounts
            .Where(account => account.UserId == userId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(account => account.Points, account => account.Points + points)
                .SetProperty(account => account.LifetimePoints, account => account.LifetimePoints + points),
                cancellationToken) == 1;

    /// <summary>A new transaction, or null when the caller already has one open.</summary>
    private async Task<IDbContextTransaction?> BeginUnlessInTransactionAsync(CancellationToken cancellationToken) =>
        _dbContext.Database.CurrentTransaction is null
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
}

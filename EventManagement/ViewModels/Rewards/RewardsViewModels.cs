using EventManagement.Models.Rewards;

namespace EventManagement.ViewModels.Rewards;

/// <param name="MinimumLifetimePoints">Lifetime points needed to reach this tier.</param>
public sealed record RewardTier(string Name, int MinimumLifetimePoints);

public class RewardHistoryItem
{
    public DateTimeOffset CreatedAt { get; set; }

    public string Description { get; set; } = string.Empty;

    public int Points { get; set; }

    public RewardTransactionKind Kind { get; set; }

    public int? OrderId { get; set; }

    public int? EventId { get; set; }
}

/// <summary>The My rewards page.</summary>
public class RewardsViewModel
{
    public int Points { get; set; }

    public int LifetimePoints { get; set; }

    /// <summary>What the current balance is worth at checkout, in whole redemptions.</summary>
    public decimal RedeemableValue { get; set; }

    public RewardTier Tier { get; set; } = null!;

    public RewardTier? NextTier { get; set; }

    public int PointsToNextTier => NextTier is null ? 0 : NextTier.MinimumLifetimePoints - LifetimePoints;

    /// <summary>Progress from the current tier to the next, 0 to 100.</summary>
    public int NextTierPercent => NextTier is null
        ? 100
        : (int)Math.Round(100.0 * (LifetimePoints - Tier.MinimumLifetimePoints) /
                          (NextTier.MinimumLifetimePoints - Tier.MinimumLifetimePoints));

    public IReadOnlyList<RewardHistoryItem> History { get; set; } = [];
}

/// <summary>The checkout page: the cart plus how many points can be used on it.</summary>
public class CheckoutViewModel
{
    public EventManagement.ViewModels.Cart.CartViewModel Cart { get; set; } = null!;

    public int PointsBalance { get; set; }

    /// <summary>The most points (a multiple of the redemption size) usable on this order.</summary>
    public int MaxRedeemablePoints { get; set; }
}

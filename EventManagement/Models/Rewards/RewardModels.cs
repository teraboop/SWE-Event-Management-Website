using EventManagement.Data;

namespace EventManagement.Models.Rewards;

public enum RewardTransactionKind
{
    /// <summary>Points earned for money paid on an order.</summary>
    Purchase = 0,

    /// <summary>Points earned for reviewing an event the user attended.</summary>
    Review = 1,

    /// <summary>Points spent for a discount on an order (negative).</summary>
    Redemption = 2
}

/// <summary>
/// A user's current points. Kept as its own row, rather than a column on the
/// user, so balance changes never collide with Identity saving the user's
/// profile. Only RewardService changes it, with conditional UPDATEs, and a
/// check constraint keeps it from going negative.
/// </summary>
public class RewardAccount
{
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    /// <summary>Points available to spend.</summary>
    public int Points { get; set; }

    /// <summary>Every point ever earned (spending doesn't lower it). Decides the tier.</summary>
    public int LifetimePoints { get; set; }
}

/// <summary>
/// One line of a user's points history. The account balance always equals
/// the sum of these, which makes the history easy to audit and explain.
/// </summary>
public class RewardTransaction
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    /// <summary>Positive when earned, negative when spent.</summary>
    public int Points { get; set; }

    public RewardTransactionKind Kind { get; set; }

    public required string Description { get; set; }

    /// <summary>
    /// The order this came from, if any. A plain id rather than a foreign key,
    /// so history survives if the order is removed.
    /// </summary>
    public int? OrderId { get; set; }

    /// <summary>The reviewed event, for review points. A plain id, like OrderId.</summary>
    public int? EventId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

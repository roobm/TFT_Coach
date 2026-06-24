namespace TFTCoach.Domain.Entities;

/// <summary>
/// A point-in-time record of a player's ranked standing in a single TFT queue,
/// captured each time their rank is fetched. Builds an accurate climb history
/// over time, since Riot's API does not expose historical rank/LP.
/// </summary>
public sealed class RankSnapshot
{
    /// <summary>Riot queue identifier, e.g. <c>RANKED_TFT</c>.</summary>
    public required string QueueType { get; init; }

    /// <summary>Tier name, e.g. <c>GOLD</c>. Null when unranked.</summary>
    public string? Tier { get; init; }

    /// <summary>Division within the tier, e.g. <c>II</c>. Null for Master+ tiers.</summary>
    public string? Division { get; init; }

    /// <summary>League points within the current division (0..100).</summary>
    public int LeaguePoints { get; init; }

    /// <summary>Wins (1st-place finishes) at the time of the snapshot.</summary>
    public int Wins { get; init; }

    /// <summary>Losses (non-1st finishes) at the time of the snapshot.</summary>
    public int Losses { get; init; }

    /// <summary>When the snapshot was recorded (UTC).</summary>
    public DateTimeOffset RecordedAt { get; init; }
}

namespace TFTCoach.Domain.Entities;

/// <summary>
/// The highest achieved Ranked TFT standing in a specific TFT set.
/// </summary>
public sealed class SeasonBestRank
{
    /// <summary>TFT set number (season).</summary>
    public int SetNumber { get; init; }

    /// <summary>Tier name, e.g. <c>EMERALD</c>.</summary>
    public required string Tier { get; init; }

    /// <summary>Division within the tier, e.g. <c>II</c>. Null for Master+.</summary>
    public string? Division { get; init; }

    /// <summary>League points at the best achieved rank.</summary>
    public int LeaguePoints { get; init; }

    /// <summary>When this best rank snapshot was recorded (UTC).</summary>
    public DateTimeOffset RecordedAt { get; init; }
}

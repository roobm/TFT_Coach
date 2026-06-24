namespace TFTCoach.Domain.Entities;

/// <summary>
/// A player's ranked standing in a single TFT queue (e.g. Ranked TFT,
/// Double Up), as reported by Riot's TFT-League-V1 endpoint.
/// </summary>
public sealed class RankedEntry
{
    /// <summary>Riot queue identifier, e.g. <c>RANKED_TFT</c> or <c>RANKED_TFT_DOUBLE_UP</c>.</summary>
    public required string QueueType { get; init; }

    /// <summary>Tier name, e.g. <c>GOLD</c>. Null for unranked or rated (turbo) queues.</summary>
    public string? Tier { get; init; }

    /// <summary>Division within the tier, e.g. <c>II</c>. Null for Master+ tiers and rated queues.</summary>
    public string? Division { get; init; }

    /// <summary>League points within the current division (0..100).</summary>
    public int LeaguePoints { get; init; }

    /// <summary>Number of games finished 1st (Ranked TFT counts a win as a 1st-place finish).</summary>
    public int Wins { get; init; }

    /// <summary>Number of games not finished 1st.</summary>
    public int Losses { get; init; }

    /// <summary>True when the player is currently on a hot streak.</summary>
    public bool HotStreak { get; init; }

    /// <summary>Total games played in this queue.</summary>
    public int TotalGames => Wins + Losses;

    /// <summary>Win rate (0..1) across this queue, or 0 when no games are recorded.</summary>
    public double WinRate => TotalGames == 0 ? 0 : (double)Wins / TotalGames;
}

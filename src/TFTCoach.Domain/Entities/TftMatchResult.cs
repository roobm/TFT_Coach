namespace TFTCoach.Domain.Entities;

/// <summary>
/// A completed TFT match for a single participant, as returned by the Riot
/// TFT-Match-V1 API. Pass-through model — not persisted in this slice.
/// </summary>
public sealed class TftMatchResult
{
    public required string MatchId { get; init; }

    public DateTimeOffset PlayedAt { get; init; }

    /// <summary>Game length in seconds.</summary>
    public double DurationSeconds { get; init; }

    /// <summary>Riot queue id (e.g. 1100 = Ranked TFT, 1160 = Ranked Double Up). 0 when unknown.</summary>
    public int QueueId { get; init; }

    /// <summary>TFT set (season) number this match was played in. 0 when unknown.</summary>
    public int SetNumber { get; init; }

    /// <summary>TFT set/patch identifier reported by Riot (e.g. game version).</summary>
    public string? GameVersion { get; init; }

    public int Placement { get; init; }

    public int Level { get; init; }

    public int PlayersEliminated { get; init; }

    /// <summary>Gold left unspent when the player was eliminated (economy signal).</summary>
    public int GoldLeft { get; init; }

    /// <summary>The last stage/round the player reached before elimination (tempo signal).</summary>
    public int LastRound { get; init; }

    /// <summary>Total combat damage the player dealt to other players (board-strength signal).</summary>
    public int TotalDamageToPlayers { get; init; }

    /// <summary>Active traits with their tier/style, highest first.</summary>
    public IReadOnlyList<string> Traits { get; init; } = [];

    public IReadOnlyList<string> Augments { get; init; } = [];

    /// <summary>Board units with star level and equipped items.</summary>
    public IReadOnlyList<UnitDetail> Units { get; init; } = [];
}

/// <summary>A single board unit with its star level (tier) and equipped items.</summary>
public sealed record UnitDetail
{
    /// <summary>Riot character id, e.g. <c>TFT17_Briar</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Star level (1–3); 0 when unknown.</summary>
    public int Tier { get; init; }

    /// <summary>Equipped item names, e.g. <c>TFT_Item_InfinityEdge</c>.</summary>
    public IReadOnlyList<string> Items { get; init; } = [];
}

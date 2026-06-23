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

    /// <summary>TFT set/patch identifier reported by Riot (e.g. game version).</summary>
    public string? GameVersion { get; init; }

    public int Placement { get; init; }

    public int Level { get; init; }

    public int PlayersEliminated { get; init; }

    /// <summary>Active traits with their tier/style, highest first.</summary>
    public IReadOnlyList<string> Traits { get; init; } = [];

    public IReadOnlyList<string> Augments { get; init; } = [];

    public IReadOnlyList<string> Units { get; init; } = [];
}

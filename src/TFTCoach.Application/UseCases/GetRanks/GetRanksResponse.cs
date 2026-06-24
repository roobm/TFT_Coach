using TFTCoach.Domain.Entities;

namespace TFTCoach.Application.UseCases.GetRanks;

/// <summary>
/// A player's ranked standing across their TFT queues, plus any recorded
/// snapshot history used to chart their climb over time.
/// </summary>
public sealed class GetRanksResponse
{
    public required string Puuid { get; init; }

    public required IReadOnlyList<RankedEntry> Entries { get; init; }

    /// <summary>Recorded rank snapshots (oldest first), across all queues. May be empty.</summary>
    public required IReadOnlyList<RankSnapshot> History { get; init; }
}

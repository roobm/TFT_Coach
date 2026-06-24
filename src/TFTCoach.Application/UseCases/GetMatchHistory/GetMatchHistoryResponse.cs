using TFTCoach.Domain.Entities;

namespace TFTCoach.Application.UseCases.GetMatchHistory;

/// <summary>
/// A player's recent matches.
/// </summary>
public sealed class GetMatchHistoryResponse
{
    public required string Puuid { get; init; }

    public required IReadOnlyList<TftMatchResult> Matches { get; init; }

    /// <summary>The player's current (latest) TFT set, so the UI can build the season picker.</summary>
    public int CurrentSet { get; init; }

    /// <summary>The TFT set the returned matches belong to.</summary>
    public int Set { get; init; }
}

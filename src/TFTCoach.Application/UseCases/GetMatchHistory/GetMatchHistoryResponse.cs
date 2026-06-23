using TFTCoach.Domain.Entities;

namespace TFTCoach.Application.UseCases.GetMatchHistory;

/// <summary>
/// A player's recent matches.
/// </summary>
public sealed class GetMatchHistoryResponse
{
    public required string Puuid { get; init; }

    public required IReadOnlyList<TftMatchResult> Matches { get; init; }
}

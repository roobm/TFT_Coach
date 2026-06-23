using TFTCoach.Domain.Entities;

namespace TFTCoach.Application.UseCases.AnalyzeMatchHistory;

/// <summary>
/// Request to generate insights from a set of matches.
/// </summary>
public sealed class AnalyzeMatchHistoryRequest
{
    public required IReadOnlyList<TftMatchResult> Matches { get; init; }

    /// <summary>
    /// Optional PUUID of the player. When provided, the analysed matches are persisted
    /// (best-effort) so results can be tracked over time.
    /// </summary>
    public string? Puuid { get; init; }
}

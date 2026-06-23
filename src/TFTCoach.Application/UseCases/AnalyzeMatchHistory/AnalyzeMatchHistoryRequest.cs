using TFTCoach.Domain.Entities;

namespace TFTCoach.Application.UseCases.AnalyzeMatchHistory;

/// <summary>
/// Request to generate insights from a set of matches.
/// </summary>
public sealed class AnalyzeMatchHistoryRequest
{
    public required IReadOnlyList<TftMatchResult> Matches { get; init; }
}

namespace TFTCoach.Application.UseCases.AnalyzeMatchHistory;

/// <summary>
/// LM Studio-generated insights about a player's recent matches.
/// </summary>
public sealed class AnalyzeMatchHistoryResponse
{
    public required string Insights { get; init; }

    public required int MatchesAnalyzed { get; init; }
}

using TFTCoach.Domain.Entities;

namespace TFTCoach.Application.UseCases.AnalyzeMatchHistory;

/// <summary>
/// In-depth, structured analysis of a player's recent matches. Numeric fields are
/// computed deterministically from the match data; the narrative fields are produced
/// by local LM Studio and grounded in those stats plus retrieved patch context.
/// </summary>
public sealed class AnalyzeMatchHistoryResponse
{
    public required int MatchesAnalyzed { get; init; }

    /// <summary>Average finishing placement (1 = best, 8 = worst).</summary>
    public required double AveragePlacement { get; init; }

    /// <summary>Fraction of games finishing in the top 4 (0..1).</summary>
    public required double Top4Rate { get; init; }

    /// <summary>Fraction of games finishing first (0..1).</summary>
    public required double WinRate { get; init; }

    /// <summary>Average player level reached.</summary>
    public required double AverageLevel { get; init; }

    /// <summary>Direction of placement results across the analysed window.</summary>
    public required PlacementTrend Trend { get; init; }

    /// <summary>Best-performing comps, most played first.</summary>
    public required IReadOnlyList<CompPerformance> TopComps { get; init; }

    public required IReadOnlyList<string> Strengths { get; init; }

    public required IReadOnlyList<string> Weaknesses { get; init; }

    public required IReadOnlyList<string> Suggestions { get; init; }

    /// <summary>Free-form narrative summary from local inference.</summary>
    public required string Narrative { get; init; }
}

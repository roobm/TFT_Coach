namespace TFTCoach.Domain.Entities;

/// <summary>
/// Aggregated performance for a single comp (identified by its leading traits)
/// across a set of matches.
/// </summary>
public sealed class CompPerformance
{
    /// <summary>Comp label, derived from the match's top one or two active traits.</summary>
    public required string Comp { get; init; }

    /// <summary>Number of matches played with this comp.</summary>
    public required int Games { get; init; }

    /// <summary>Average finishing placement (1 = best, 8 = worst).</summary>
    public required double AveragePlacement { get; init; }
}

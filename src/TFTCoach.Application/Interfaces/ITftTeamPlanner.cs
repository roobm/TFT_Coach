using TFTCoach.Domain.Entities;

namespace TFTCoach.Application.Interfaces;

/// <summary>
/// Provides Riot's official TFT Team Planner champion data, keyed by the
/// <c>team_planner_code</c> embedded in shareable team codes. Backed by static
/// game data (Community Dragon) and cached per set.
/// </summary>
public interface ITftTeamPlanner
{
    /// <summary>
    /// Returns the champion map for a set (e.g. <c>TFTSet17</c>), keyed by
    /// <c>team_planner_code</c>. Empty if the set is unknown or data is unavailable.
    /// </summary>
    Task<IReadOnlyDictionary<int, TeamPlannerChampion>> GetChampionsAsync(
        string setId,
        CancellationToken cancellationToken = default);
}

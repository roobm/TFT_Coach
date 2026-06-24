namespace TFTCoach.Domain.Entities;

/// <summary>
/// A single champion as it appears in Riot's TFT Team Planner data, keyed by the
/// official <c>team_planner_code</c> used inside shareable team codes.
/// </summary>
public sealed class TeamPlannerChampion
{
    /// <summary>Riot internal id, e.g. <c>TFT17_Aatrox</c>.</summary>
    public required string CharacterId { get; init; }

    /// <summary>In-game display name, e.g. <c>Aatrox</c> or <c>Nunu &amp; Willump</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Gold cost / tier (1–5).</summary>
    public required int Cost { get; init; }
}

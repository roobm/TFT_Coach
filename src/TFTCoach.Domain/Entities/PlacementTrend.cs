namespace TFTCoach.Domain.Entities;

/// <summary>
/// Direction of a player's placement results over a window of matches.
/// Lower placements are better, so "improving" means recent placements are lower.
/// </summary>
public enum PlacementTrend
{
    Improving,
    Declining,
    Stable,
}

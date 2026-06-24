namespace TFTCoach.Domain.Entities;

/// <summary>
/// The current TFT meta for a given set + patch: the strongest compositions the
/// coach should prefer when advising. Tagged with the patch version so advice
/// stays tied to a known game state.
/// </summary>
public sealed class MetaSnapshot
{
    public required string Set { get; init; }
    public required string Patch { get; init; }
    public IReadOnlyList<MetaComp> Comps { get; init; } = [];
}

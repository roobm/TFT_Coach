namespace TFTCoach.Domain.Entities;

/// <summary>
/// One meta composition the coach can steer a player toward: its carries, the
/// items those carries want, and a short note on how to play it.
/// </summary>
public sealed class MetaComp
{
    public required string Name { get; init; }
    public string Tier { get; init; } = "";
    public string Playstyle { get; init; } = "";
    public IReadOnlyList<string> Carries { get; init; } = [];
    public IReadOnlyList<string> KeyItems { get; init; } = [];
    public string? Notes { get; init; }
}

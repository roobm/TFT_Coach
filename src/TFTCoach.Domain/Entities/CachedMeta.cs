namespace TFTCoach.Domain.Entities;

/// <summary>
/// A persisted <see cref="MetaSnapshot"/> together with the time it was computed,
/// used to serve the last known meta instantly across restarts and to decide
/// whether a background refresh is due.
/// </summary>
public sealed class CachedMeta
{
    public required MetaSnapshot Snapshot { get; init; }
    public required DateTimeOffset ComputedAt { get; init; }
}

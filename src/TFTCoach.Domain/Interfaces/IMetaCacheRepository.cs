using TFTCoach.Domain.Entities;

namespace TFTCoach.Domain.Interfaces;

/// <summary>
/// Persists computed meta snapshots, grouped by set, so the live ladder meta
/// survives restarts. One row is kept per set (the latest computation for that
/// set); reads return the most recently computed set.
/// </summary>
public interface IMetaCacheRepository
{
    /// <summary>Upserts the snapshot for its set (one row per set).</summary>
    Task SaveAsync(MetaSnapshot snapshot, CancellationToken cancellationToken = default);

    /// <summary>Returns the most recently computed snapshot, or null if none cached.</summary>
    Task<CachedMeta?> GetLatestAsync(CancellationToken cancellationToken = default);
}

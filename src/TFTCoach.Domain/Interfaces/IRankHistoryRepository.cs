using TFTCoach.Domain.Entities;

namespace TFTCoach.Domain.Interfaces;

/// <summary>
/// Persists point-in-time snapshots of a player's ranked standing so the app
/// can reconstruct an accurate climb history over time. Keyed by PUUID until
/// user auth exists.
/// </summary>
public interface IRankHistoryRepository
{
    /// <summary>
    /// Records the given ranked entries as snapshots for a PUUID. Implementations
    /// should skip writing a new snapshot when it is identical to the most recent
    /// one for the same queue, so repeated fetches do not create duplicate points.
    /// </summary>
    Task RecordAsync(string puuid, IReadOnlyList<RankedEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the recorded snapshots for a PUUID, oldest first, across all queues.
    /// </summary>
    Task<IReadOnlyList<RankSnapshot>> GetHistoryAsync(string puuid, CancellationToken cancellationToken = default);
}

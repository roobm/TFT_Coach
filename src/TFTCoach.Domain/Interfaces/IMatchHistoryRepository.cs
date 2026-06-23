using TFTCoach.Domain.Entities;

namespace TFTCoach.Domain.Interfaces;

/// <summary>
/// Persists a player's TFT match results so analysis can track results over time.
/// Keyed by PUUID until user auth exists.
/// </summary>
public interface IMatchHistoryRepository
{
    /// <summary>
    /// Stores the given matches for a PUUID. Implementations must be idempotent per
    /// <c>(puuid, matchId)</c> so re-analysing the same games does not create duplicates.
    /// </summary>
    Task SaveAsync(string puuid, IReadOnlyList<TftMatchResult> matches, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent stored matches for a PUUID, newest first.
    /// </summary>
    Task<IReadOnlyList<TftMatchResult>> GetByPuuidAsync(string puuid, int limit, CancellationToken cancellationToken = default);
}

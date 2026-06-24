using TFTCoach.Domain.Entities;

namespace TFTCoach.Application.Interfaces;

/// <summary>
/// Reads TFT match data from the Riot API.
/// </summary>
public interface IRiotMatchClient
{
    /// <summary>
    /// Resolves a Riot ID (<c>gameName#tagLine</c>) to its PUUID.
    /// </summary>
    Task<string> ResolvePuuidAsync(string gameName, string tagLine, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent match ids for a PUUID, newest first. Cheap relative to
    /// fetching full match detail, so callers can diff against a cache before pulling details.
    /// </summary>
    Task<IReadOnlyList<string>> GetRecentMatchIdsAsync(string puuid, int count, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the full detail for a single match from the player's perspective,
    /// or <c>null</c> if the player did not take part in it.
    /// </summary>
    Task<TftMatchResult?> GetMatchAsync(string matchId, string puuid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every participant's board in a match (placements 1-8), so callers can
    /// aggregate a whole lobby rather than a single player. Used when computing the
    /// live meta from sampled ladder games.
    /// </summary>
    Task<IReadOnlyList<TftMatchResult>> GetMatchParticipantsAsync(string matchId, CancellationToken cancellationToken = default);
}

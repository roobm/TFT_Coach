using TFTCoach.Domain.Entities;

namespace TFTCoach.Application.Interfaces;

/// <summary>
/// Reads a player's ranked standing from the Riot TFT-League-V1 API.
/// Platform-routed (e.g. <c>euw1</c>), unlike the regional match endpoints.
/// </summary>
public interface IRiotLeagueClient
{
    /// <summary>
    /// Returns the player's ranked entries (one per ranked queue they have played),
    /// or an empty list when the player is unranked in every queue.
    /// </summary>
    Task<IReadOnlyList<RankedEntry>> GetRankedEntriesAsync(string puuid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the PUUIDs of the current Ranked TFT Challenger players, ordered by
    /// league points (highest first), capped at <paramref name="maxPlayers"/>.
    /// Used to sample the highest-elo ladder when computing the live meta.
    /// </summary>
    Task<IReadOnlyList<string>> GetChallengerPuuidsAsync(int maxPlayers, CancellationToken cancellationToken = default);
}

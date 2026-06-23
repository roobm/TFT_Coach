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
    /// Returns the most recent matches for a PUUID, newest first.
    /// </summary>
    Task<IReadOnlyList<TftMatchResult>> GetRecentMatchesAsync(string puuid, int count, CancellationToken cancellationToken = default);
}

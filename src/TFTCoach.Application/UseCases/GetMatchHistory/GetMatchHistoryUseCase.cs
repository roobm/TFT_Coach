using Microsoft.Extensions.Logging;
using TFTCoach.Application.Interfaces;
using TFTCoach.Domain.Entities;
using TFTCoach.Domain.Interfaces;

namespace TFTCoach.Application.UseCases.GetMatchHistory;

/// <summary>
/// Resolves a Riot ID to a PUUID and returns the player's recent TFT matches.
/// Already-stored matches are served from the database so the throttled Riot API
/// (100 req / 2 min) is only called for match ids we have not cached yet.
/// </summary>
public sealed class GetMatchHistoryUseCase(
    IRiotMatchClient riotClient,
    IMatchHistoryRepository matchHistoryRepository,
    ILogger<GetMatchHistoryUseCase> logger)
{
    public async Task<GetMatchHistoryResponse> ExecuteAsync(
        GetMatchHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var count = Math.Clamp(request.Count, 1, 200);

        var puuid = await riotClient.ResolvePuuidAsync(request.GameName, request.TagLine, cancellationToken);
        var matchIds = await riotClient.GetRecentMatchIdsAsync(puuid, count, cancellationToken);
        if (matchIds.Count == 0)
        {
            return new GetMatchHistoryResponse { Puuid = puuid, Matches = [] };
        }

        var cached = await LoadCachedAsync(puuid, matchIds, cancellationToken);

        // Only pull detail from Riot for ids we have not already stored.
        var fetched = new List<TftMatchResult>();
        foreach (var matchId in matchIds)
        {
            if (cached.ContainsKey(matchId))
            {
                continue;
            }

            var match = await riotClient.GetMatchAsync(matchId, puuid, cancellationToken);
            if (match is not null)
            {
                fetched.Add(match);
                cached[matchId] = match;
            }
        }

        await PersistNewAsync(puuid, fetched, cancellationToken);

        // Return in Riot's order (newest first), keeping only matches we could resolve.
        var matches = matchIds
            .Select(id => cached.GetValueOrDefault(id))
            .Where(match => match is not null)
            .Select(match => match!)
            .ToList();

        logger.LogInformation(
            "Returned {Total} matches for {GameName}#{TagLine} ({Cached} cached, {Fetched} from Riot)",
            matches.Count, request.GameName, request.TagLine, matches.Count - fetched.Count, fetched.Count);

        return new GetMatchHistoryResponse
        {
            Puuid = puuid,
            Matches = matches,
        };
    }

    // Cache reads are best-effort: if the store is unavailable we simply fetch everything from Riot.
    private async Task<Dictionary<string, TftMatchResult>> LoadCachedAsync(
        string puuid,
        IReadOnlyList<string> matchIds,
        CancellationToken cancellationToken)
    {
        try
        {
            var stored = await matchHistoryRepository.GetByMatchIdsAsync(puuid, matchIds, cancellationToken);
            return stored.ToDictionary(match => match.MatchId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read cached matches; fetching all from Riot.");
            return [];
        }
    }

    // Persisting fetched matches is best-effort: a storage failure must not fail the request.
    private async Task PersistNewAsync(
        string puuid,
        IReadOnlyList<TftMatchResult> matches,
        CancellationToken cancellationToken)
    {
        if (matches.Count == 0)
        {
            return;
        }

        try
        {
            await matchHistoryRepository.SaveAsync(puuid, matches, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to cache fetched matches; continuing.");
        }
    }
}

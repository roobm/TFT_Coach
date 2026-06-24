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
        // Count <= 0 is the "fetch everything" sentinel: page through all of the
        // player's match ids. A positive count is clamped to a single page request.
        var count = request.Count <= 0 ? int.MaxValue : Math.Clamp(request.Count, 1, 200);

        // When a specific season is requested we must page through every match id
        // (the target set may be far older than the newest games); otherwise the
        // newest `count` ids already cover the current set.
        var idFetchCount = request.Set > 0 ? int.MaxValue : count;

        var puuid = await riotClient.ResolvePuuidAsync(request.GameName, request.TagLine, cancellationToken);
        var matchIds = await riotClient.GetRecentMatchIdsAsync(puuid, idFetchCount, cancellationToken);
        if (matchIds.Count == 0)
        {
            return new GetMatchHistoryResponse { Puuid = puuid, Matches = [] };
        }

        var cached = await LoadCachedAsync(puuid, matchIds, cancellationToken);

        // Riot returns ids newest-first, but a match's set is only known after we
        // load its detail. The current set is the newest match's set; the target
        // set is either an explicitly requested season or the current one. We walk
        // newest→oldest, skipping newer sets and stopping once we pass below the
        // target, so we never re-fetch the player's entire multi-season history
        // unless they explicitly ask for an old season.
        var currentSet = await ResolveCurrentSetAsync(puuid, matchIds, cached, cancellationToken);
        var targetSet = request.Set > 0 ? request.Set : currentSet;

        var fetched = new List<TftMatchResult>();
        var matches = new List<TftMatchResult>();
        foreach (var matchId in matchIds)
        {
            if (!cached.TryGetValue(matchId, out var match))
            {
                match = await riotClient.GetMatchAsync(matchId, puuid, cancellationToken);
                if (match is null)
                {
                    continue;
                }

                fetched.Add(match);
                cached[matchId] = match;
            }

            // Legacy cached rows have no set recorded (0); treat them as current-season.
            var effectiveSet = match.SetNumber == 0 ? currentSet : match.SetNumber;
            if (targetSet > 0 && effectiveSet > 0)
            {
                if (effectiveSet > targetSet)
                {
                    continue; // newer than the target season — skip it.
                }

                if (effectiveSet < targetSet)
                {
                    break; // reached an older season — everything beyond is older too.
                }
            }

            matches.Add(match);
            if (matches.Count >= count)
            {
                break;
            }
        }

        await PersistNewAsync(puuid, fetched, cancellationToken);

        logger.LogInformation(
            "Returned {Total} set-{Set} matches for {GameName}#{TagLine} ({Fetched} from Riot)",
            matches.Count, targetSet, request.GameName, request.TagLine, fetched.Count);

        return new GetMatchHistoryResponse
        {
            Puuid = puuid,
            Matches = matches,
            CurrentSet = currentSet,
            Set = targetSet,
        };
    }

    // The current set is the set of the player's newest match. If that match is
    // not cached (or was stored before set tracking existed), fetch it once so we
    // have a reliable boundary; the fetched match is persisted for next time.
    private async Task<int> ResolveCurrentSetAsync(
        string puuid,
        IReadOnlyList<string> matchIds,
        Dictionary<string, TftMatchResult> cached,
        CancellationToken cancellationToken)
    {
        var newestId = matchIds[0];
        if (cached.TryGetValue(newestId, out var newest) && newest.SetNumber > 0)
        {
            return newest.SetNumber;
        }

        var fetchedNewest = await riotClient.GetMatchAsync(newestId, puuid, cancellationToken);
        if (fetchedNewest is null)
        {
            return 0;
        }

        cached[newestId] = fetchedNewest;
        await PersistNewAsync(puuid, [fetchedNewest], cancellationToken);
        return fetchedNewest.SetNumber;
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

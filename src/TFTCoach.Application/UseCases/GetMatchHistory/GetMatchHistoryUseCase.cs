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

        var puuid = await riotClient.ResolvePuuidAsync(request.GameName, request.TagLine, cancellationToken);
        var matchIds = await riotClient.GetRecentMatchIdsAsync(puuid, count, cancellationToken);
        if (matchIds.Count == 0)
        {
            return new GetMatchHistoryResponse { Puuid = puuid, Matches = [] };
        }

        var cached = await LoadCachedAsync(puuid, matchIds, cancellationToken);

        // Walk matches newest-first, fetching detail only for ids we have not stored.
        // Riot returns ids in reverse-chronological order, so once we reach a match
        // from an older TFT set we can stop: everything beyond it is a previous season.
        // This keeps results scoped to the current set and avoids re-fetching the
        // player's entire (multi-season) history from the throttled Riot API.
        var currentSet = await ResolveCurrentSetAsync(puuid, matchIds, cached, cancellationToken);

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

            // Stop at the first match we can confidently place in an earlier set.
            // Matches with an unknown set (0) are kept — legacy cached rows have no
            // set recorded yet and are, in practice, current-season games.
            if (currentSet > 0 && match.SetNumber > 0 && match.SetNumber != currentSet)
            {
                break;
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
            matches.Count, currentSet, request.GameName, request.TagLine, fetched.Count);

        return new GetMatchHistoryResponse
        {
            Puuid = puuid,
            Matches = matches,
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

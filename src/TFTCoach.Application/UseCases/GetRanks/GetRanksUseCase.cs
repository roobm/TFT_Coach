using Microsoft.Extensions.Logging;
using TFTCoach.Application.Common;
using TFTCoach.Application.Interfaces;
using TFTCoach.Domain.Entities;
using TFTCoach.Domain.Interfaces;

namespace TFTCoach.Application.UseCases.GetRanks;

/// <summary>
/// Resolves a Riot ID to a PUUID and returns the player's ranked standing
/// across their TFT queues. Records a snapshot of the current standing so the
/// app can chart the player's climb over time, and returns the recorded history.
/// Stateless — a single account lookup plus one platform-routed League-V1 call.
/// </summary>
public sealed class GetRanksUseCase(
    IRiotMatchClient riotClient,
    IRiotLeagueClient leagueClient,
    IRankHistoryRepository rankHistoryRepository,
    ILogger<GetRanksUseCase> logger)
{
    public async Task<RequestResult<GetRanksResponse>> ExecuteAsync(
        GetRanksRequest request,
        CancellationToken cancellationToken = default)
    {
        string puuid;
        try
        {
            puuid = await riotClient.ResolvePuuidAsync(request.GameName, request.TagLine, cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            return RequestResult<GetRanksResponse>.Fail(
                ApiErrors.NotFound("not_found.player", ex.Message));
        }

        var entries = await leagueClient.GetRankedEntriesAsync(puuid, cancellationToken);

        await RecordSnapshotsAsync(puuid, entries, cancellationToken);
        var history = await LoadHistoryAsync(puuid, cancellationToken);

        logger.LogInformation(
            "Returned {Count} ranked entries ({History} history points) for {GameName}#{TagLine}",
            entries.Count, history.Count, request.GameName, request.TagLine);

        return RequestResult<GetRanksResponse>.Ok(
            new GetRanksResponse
            {
                Puuid = puuid,
                Entries = entries,
                History = history,
            });
    }

    // Recording is best-effort: a storage failure must not fail the request.
    private async Task RecordSnapshotsAsync(
        string puuid,
        IReadOnlyList<RankedEntry> entries,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return;
        }

        try
        {
            await rankHistoryRepository.RecordAsync(puuid, entries, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record rank snapshots; continuing.");
        }
    }

    // History reads are best-effort: if the store is unavailable we return no history.
    private async Task<IReadOnlyList<RankSnapshot>> LoadHistoryAsync(
        string puuid,
        CancellationToken cancellationToken)
    {
        try
        {
            return await rankHistoryRepository.GetHistoryAsync(puuid, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read rank history; returning none.");
            return [];
        }
    }
}

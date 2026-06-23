using Microsoft.Extensions.Logging;
using TFTCoach.Application.Interfaces;

namespace TFTCoach.Application.UseCases.GetMatchHistory;

/// <summary>
/// Resolves a Riot ID to a PUUID and returns the player's recent TFT matches.
/// </summary>
public sealed class GetMatchHistoryUseCase(
    IRiotMatchClient riotClient,
    ILogger<GetMatchHistoryUseCase> logger)
{
    public async Task<GetMatchHistoryResponse> ExecuteAsync(
        GetMatchHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var count = Math.Clamp(request.Count, 1, 200);

        var puuid = await riotClient.ResolvePuuidAsync(request.GameName, request.TagLine, cancellationToken);
        var matches = await riotClient.GetRecentMatchesAsync(puuid, count, cancellationToken);

        logger.LogInformation(
            "Fetched {Count} matches for {GameName}#{TagLine}",
            matches.Count, request.GameName, request.TagLine);

        return new GetMatchHistoryResponse
        {
            Puuid = puuid,
            Matches = matches,
        };
    }
}

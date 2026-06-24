using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TFTCoach.Application.Interfaces;
using TFTCoach.Domain.Entities;

namespace TFTCoach.Infrastructure.RiotApi;

/// <summary>
/// Reads TFT match data from the Riot API using Account-V1 (Riot ID → PUUID)
/// and TFT-Match-V1 (match ids + match detail). Regional routing only
/// (e.g. <c>europe</c>).
/// </summary>
public sealed class RiotMatchClient(
    HttpClient httpClient,
    string apiKey,
    ILogger<RiotMatchClient> logger) : IRiotMatchClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<string> ResolvePuuidAsync(string gameName, string tagLine, CancellationToken cancellationToken = default)
    {
        var url = $"riot/account/v1/accounts/by-riot-id/{Uri.EscapeDataString(gameName)}/{Uri.EscapeDataString(tagLine)}";
        try
        {
            using var request = CreateRequest(HttpMethod.Get, url);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, $"{gameName}#{tagLine}", cancellationToken);

            var account = await response.Content.ReadFromJsonAsync<AccountDto>(JsonOptions, cancellationToken);
            if (account?.Puuid is not { Length: > 0 } puuid)
            {
                throw new InvalidOperationException("Riot account response did not contain a PUUID.");
            }

            return puuid;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Failed to resolve PUUID for {GameName}#{TagLine}", gameName, tagLine);
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> GetRecentMatchIdsAsync(string puuid, int count, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetMatchIdsAsync(puuid, count, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Failed to fetch match ids for PUUID {Puuid}", puuid);
            throw;
        }
    }

    // The Riot ids endpoint returns at most 200 ids per request, so page through
    // with the start offset until we have the requested count (or run out). Pass
    // int.MaxValue to fetch every available match id.
    private async Task<List<string>> GetMatchIdsAsync(string puuid, int count, CancellationToken cancellationToken)
    {
        const int pageSize = 200;
        var ids = new List<string>(Math.Min(count, pageSize));
        var start = 0;

        while (ids.Count < count)
        {
            var pageCount = Math.Min(pageSize, count - ids.Count);
            using var request = CreateRequest(
                HttpMethod.Get,
                $"tft/match/v1/matches/by-puuid/{puuid}/ids?start={start}&count={pageCount}");
            using var response = await httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, puuid, cancellationToken);

            var page = await response.Content.ReadFromJsonAsync<List<string>>(JsonOptions, cancellationToken) ?? [];
            if (page.Count == 0)
            {
                break;
            }

            ids.AddRange(page);
            start += page.Count;

            if (page.Count < pageCount)
            {
                break;
            }
        }

        return ids;
    }

    public async Task<TftMatchResult?> GetMatchAsync(string matchId, string puuid, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"tft/match/v1/matches/{matchId}");
            using var response = await httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, matchId, cancellationToken);

            var match = await response.Content.ReadFromJsonAsync<MatchDto>(JsonOptions, cancellationToken);
            var info = match?.Info;
            var participant = info?.Participants?.FirstOrDefault(p => p.Puuid == puuid);
            if (info is null || participant is null)
            {
                return null;
            }

            return new TftMatchResult
            {
                MatchId = matchId,
                PlayedAt = DateTimeOffset.FromUnixTimeMilliseconds(info.GameDatetime),
                DurationSeconds = info.GameLength,
                QueueId = info.QueueId,
                SetNumber = info.TftSetNumber,
                GameVersion = info.GameVersion,
                Placement = participant.Placement,
                Level = participant.Level,
                PlayersEliminated = participant.PlayersEliminated,
                GoldLeft = participant.GoldLeft,
                LastRound = participant.LastRound,
                TotalDamageToPlayers = participant.TotalDamageToPlayers,
                Traits = participant.Traits?
                    .Where(t => t.TierCurrent > 0)
                    .OrderByDescending(t => t.TierCurrent)
                    .Select(t => $"{t.Name} {t.TierCurrent}")
                    .ToList() ?? [],
                Augments = participant.Augments ?? [],
                Units = participant.Units?
                    .Where(u => !string.IsNullOrEmpty(u.CharacterId))
                    .Select(u => new UnitDetail
                    {
                        Name = u.CharacterId!,
                        Tier = u.Tier,
                        Items = u.ItemNames ?? [],
                    })
                    .ToList() ?? [],
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Failed to fetch match {MatchId} for PUUID {Puuid}", matchId, puuid);
            throw;
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.Add("X-Riot-Token", apiKey);
        return request;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, string subject, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning("Riot API {Status} for {Subject}: {Body}", (int)response.StatusCode, subject, body);

        throw response.StatusCode switch
        {
            HttpStatusCode.NotFound => new KeyNotFoundException($"Riot resource not found: {subject}"),
            HttpStatusCode.TooManyRequests => new InvalidOperationException("Riot API rate limit exceeded. Try again shortly."),
            HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized =>
                new InvalidOperationException("Riot API key is missing, invalid, or expired."),
            _ => new HttpRequestException($"Riot API request failed with status {(int)response.StatusCode}."),
        };
    }

    private sealed record AccountDto([property: JsonPropertyName("puuid")] string? Puuid);

    private sealed record MatchDto(MatchInfoDto? Info);

    private sealed record MatchInfoDto(
        long GameDatetime,
        double GameLength,
        string? GameVersion,
        [property: JsonPropertyName("queue_id")] int QueueId,
        [property: JsonPropertyName("tft_set_number")] int TftSetNumber,
        List<ParticipantDto>? Participants);

    private sealed record ParticipantDto(
        string? Puuid,
        int Placement,
        int Level,
        int PlayersEliminated,
        int GoldLeft,
        int LastRound,
        int TotalDamageToPlayers,
        List<string>? Augments,
        List<TraitDto>? Traits,
        List<UnitDto>? Units);

    private sealed record TraitDto(string? Name, [property: JsonPropertyName("tier_current")] int TierCurrent);

    private sealed record UnitDto(
        [property: JsonPropertyName("character_id")] string? CharacterId,
        int Tier,
        [property: JsonPropertyName("itemNames")] List<string>? ItemNames);
}

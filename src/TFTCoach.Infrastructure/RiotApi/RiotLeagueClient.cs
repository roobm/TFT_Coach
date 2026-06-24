using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TFTCoach.Application.Interfaces;
using TFTCoach.Domain.Entities;

namespace TFTCoach.Infrastructure.RiotApi;

/// <summary>
/// Reads a player's ranked standing from the Riot TFT-League-V1 endpoint
/// (<c>tft/league/v1/by-puuid/{puuid}</c>). Platform-routed (e.g. <c>euw1</c>),
/// so it uses a different host than the regional match client.
/// </summary>
public sealed class RiotLeagueClient(
    HttpClient httpClient,
    string apiKey,
    ILogger<RiotLeagueClient> logger) : IRiotLeagueClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<RankedEntry>> GetRankedEntriesAsync(string puuid, CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"tft/league/v1/by-puuid/{puuid}");
            using var response = await httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, puuid, cancellationToken);

            var entries = await response.Content.ReadFromJsonAsync<List<LeagueEntryDto>>(JsonOptions, cancellationToken) ?? [];
            return entries
                .Where(e => !string.IsNullOrEmpty(e.QueueType))
                .Select(e => new RankedEntry
                {
                    QueueType = e.QueueType!,
                    Tier = string.IsNullOrEmpty(e.Tier) ? null : e.Tier,
                    Division = string.IsNullOrEmpty(e.Rank) ? null : e.Rank,
                    LeaguePoints = e.LeaguePoints,
                    Wins = e.Wins,
                    Losses = e.Losses,
                    HotStreak = e.HotStreak,
                })
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Failed to fetch ranked entries for PUUID {Puuid}", puuid);
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> GetChallengerPuuidsAsync(int maxPlayers, CancellationToken cancellationToken = default)
    {
        if (maxPlayers <= 0)
        {
            return [];
        }

        try
        {
            using var request = CreateRequest(HttpMethod.Get, "tft/league/v1/challenger?queue=RANKED_TFT");
            using var response = await httpClient.SendAsync(request, cancellationToken);
            await EnsureSuccessAsync(response, "challenger", cancellationToken);

            var league = await response.Content.ReadFromJsonAsync<LeagueListDto>(JsonOptions, cancellationToken);
            return league?.Entries?
                .Where(e => !string.IsNullOrEmpty(e.Puuid))
                .OrderByDescending(e => e.LeaguePoints)
                .Take(maxPlayers)
                .Select(e => e.Puuid!)
                .ToList() ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Failed to fetch challenger ladder");
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

    private sealed record LeagueEntryDto(
        [property: JsonPropertyName("queueType")] string? QueueType,
        [property: JsonPropertyName("tier")] string? Tier,
        [property: JsonPropertyName("rank")] string? Rank,
        [property: JsonPropertyName("leaguePoints")] int LeaguePoints,
        [property: JsonPropertyName("wins")] int Wins,
        [property: JsonPropertyName("losses")] int Losses,
        [property: JsonPropertyName("hotStreak")] bool HotStreak);

    private sealed record LeagueListDto(
        [property: JsonPropertyName("entries")] List<LeagueListEntryDto>? Entries);

    private sealed record LeagueListEntryDto(
        [property: JsonPropertyName("puuid")] string? Puuid,
        [property: JsonPropertyName("leaguePoints")] int LeaguePoints);
}

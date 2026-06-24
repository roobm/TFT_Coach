using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TFTCoach.Application.Interfaces;
using TFTCoach.Domain.Entities;

namespace TFTCoach.Infrastructure.Meta;

/// <summary>
/// Supplies Riot's official TFT Team Planner champion data from Community Dragon.
/// The data carries an official <c>team_planner_code</c> per champion, so decoding
/// shareable team codes is exact rather than guesswork. Fetched once per set and
/// cached; a fetch failure yields an empty map (decoding then resolves nothing)
/// rather than crashing.
/// </summary>
public sealed class CommunityDragonTeamPlanner(
    HttpClient httpClient,
    ILogger<CommunityDragonTeamPlanner> logger) : ITftTeamPlanner
{
    private const string DataUrl =
        "https://raw.communitydragon.org/latest/plugins/rcp-be-lol-game-data/global/default/v1/tftchampions-teamplanner.json";

    private static readonly IReadOnlyDictionary<int, TeamPlannerChampion> Empty =
        new Dictionary<int, TeamPlannerChampion>();

    private readonly ConcurrentDictionary<string, IReadOnlyDictionary<int, TeamPlannerChampion>> cache = new();
    private readonly SemaphoreSlim gate = new(1, 1);

    private JsonDocument? document;

    public async Task<IReadOnlyDictionary<int, TeamPlannerChampion>> GetChampionsAsync(
        string setId,
        CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(setId, out var cached))
        {
            return cached;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(setId, out cached))
            {
                return cached;
            }

            if (document is null)
            {
                await using var stream = await httpClient.GetStreamAsync(DataUrl, cancellationToken);
                document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            }

            var map = BuildSetMap(document.RootElement, setId);
            cache[setId] = map;

            logger.LogInformation(
                "Loaded TFT team planner data for {Set}: {Count} champions.", setId, map.Count);
            return map;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            // Non-fatal: decoding resolves no champions until a later retry.
            logger.LogWarning(ex, "Failed to load TFT team planner data for {Set}.", setId);
            return Empty;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Builds a <c>team_planner_code</c>→champion map for one set from the Community
    /// Dragon team-planner root element (an object keyed by set id).
    /// </summary>
    internal static IReadOnlyDictionary<int, TeamPlannerChampion> BuildSetMap(JsonElement root, string setId)
    {
        var map = new Dictionary<int, TeamPlannerChampion>();
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(setId, out var champions)
            || champions.ValueKind != JsonValueKind.Array)
        {
            return map;
        }

        foreach (var champion in champions.EnumerateArray())
        {
            if (!TryGetInt(champion, "team_planner_code", out var code))
            {
                continue;
            }

            var name = GetString(champion, "display_name");
            var characterId = GetString(champion, "character_id");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(characterId))
            {
                continue;
            }

            TryGetInt(champion, "tier", out var cost);
            map[code] = new TeamPlannerChampion
            {
                CharacterId = characterId.Trim(),
                Name = name.Trim(),
                Cost = cost,
            };
        }

        return map;
    }

    private static bool TryGetInt(JsonElement element, string property, out int value)
    {
        value = 0;
        if (!element.TryGetProperty(property, out var prop))
        {
            return false;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number => prop.TryGetInt32(out value),
            JsonValueKind.String => int.TryParse(prop.GetString(), out value),
            _ => false,
        };
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

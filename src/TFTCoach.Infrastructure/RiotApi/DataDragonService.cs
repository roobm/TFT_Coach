using System.Text.Json;
using Microsoft.Extensions.Logging;
using TFTCoach.Application.Interfaces;
using TFTCoach.Domain.Entities;

namespace TFTCoach.Infrastructure.RiotApi;

/// <summary>
/// Fetches static TFT data (champions, traits, items) from Community Dragon and
/// chunks it into <see cref="PatchDocument"/>s ready for embedding.
/// </summary>
public sealed class DataDragonService(
    HttpClient httpClient,
    ILogger<DataDragonService> logger) : IPatchContentProvider
{
    // Community Dragon serves consolidated TFT static data as a single JSON file.
    private const string DataUrl = "https://raw.communitydragon.org/latest/cdragon/tft/en_us.json";

    public async Task<IReadOnlyList<PatchDocument>> GetDocumentsAsync(string patch, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = await httpClient.GetStreamAsync(DataUrl, cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = json.RootElement;

            var documents = new List<PatchDocument>();
            documents.AddRange(ParseItems(root, patch));

            var latestSet = SelectLatestSet(root);
            if (latestSet is { } set)
            {
                documents.AddRange(ParseChampions(set, patch));
                documents.AddRange(ParseTraits(set, patch));
            }

            logger.LogInformation("Parsed {Count} TFT content chunks for patch {Patch}", documents.Count, patch);
            return documents;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogError(ex, "Failed to fetch or parse TFT data for patch {Patch}", patch);
            throw;
        }
    }

    private static JsonElement? SelectLatestSet(JsonElement root)
    {
        if (!root.TryGetProperty("setData", out var setData) || setData.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        JsonElement? latest = null;
        var highest = double.MinValue;
        foreach (var set in setData.EnumerateArray())
        {
            var number = set.TryGetProperty("number", out var n) && n.TryGetDouble(out var value) ? value : 0;
            if (number >= highest)
            {
                highest = number;
                latest = set;
            }
        }

        return latest;
    }

    private static IEnumerable<PatchDocument> ParseChampions(JsonElement set, string patch)
    {
        if (!set.TryGetProperty("champions", out var champions) || champions.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var champion in champions.EnumerateArray())
        {
            var name = GetString(champion, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var cost = champion.TryGetProperty("cost", out var c) && c.TryGetInt32(out var costValue) ? costValue : 0;
            var traits = ReadStringArray(champion, "traits");

            string? abilityName = null;
            string? abilityDesc = null;
            if (champion.TryGetProperty("ability", out var ability) && ability.ValueKind == JsonValueKind.Object)
            {
                abilityName = GetString(ability, "name");
                abilityDesc = GetString(ability, "desc");
            }

            yield return new PatchDocument
            {
                Patch = patch,
                Category = "champion",
                Content = PatchNoteChunker.Champion(name, cost, traits, abilityName, abilityDesc),
            };
        }
    }

    private static IEnumerable<PatchDocument> ParseTraits(JsonElement set, string patch)
    {
        if (!set.TryGetProperty("traits", out var traits) || traits.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var trait in traits.EnumerateArray())
        {
            var name = GetString(trait, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            yield return new PatchDocument
            {
                Patch = patch,
                Category = "trait",
                Content = PatchNoteChunker.Trait(name, GetString(trait, "desc")),
            };
        }
    }

    private static IEnumerable<PatchDocument> ParseItems(JsonElement root, string patch)
    {
        if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var item in items.EnumerateArray())
        {
            var name = GetString(item, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            yield return new PatchDocument
            {
                Patch = patch,
                Category = "item",
                Content = PatchNoteChunker.Item(name, GetString(item, "desc")),
            };
        }
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } value)
            {
                result.Add(value);
            }
        }

        return result;
    }
}

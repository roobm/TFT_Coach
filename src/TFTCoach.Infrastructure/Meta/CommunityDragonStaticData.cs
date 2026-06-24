using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TFTCoach.Application.Interfaces;

namespace TFTCoach.Infrastructure.Meta;

/// <summary>
/// Resolves Riot API names into in-game display names using the consolidated
/// Community Dragon TFT static-data file (the same source <c>DataDragonService</c>
/// uses). Loaded once and cached; if the fetch fails, resolution degrades to a
/// best-effort cleanup of the raw identifier so coaching never breaks.
/// </summary>
public sealed partial class CommunityDragonStaticData(
    HttpClient httpClient,
    ILogger<CommunityDragonStaticData> logger) : ITftStaticData
{
    private const string DataUrl = "https://raw.communitydragon.org/latest/cdragon/tft/en_us.json";

    private static readonly IReadOnlyDictionary<string, string> Empty =
        new Dictionary<string, string>();

    private readonly SemaphoreSlim gate = new(1, 1);
    private volatile bool loaded;

    private IReadOnlyDictionary<string, string> items = Empty;
    private IReadOnlyDictionary<string, string> champions = Empty;
    private IReadOnlyDictionary<string, string> traits = Empty;

    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (loaded)
        {
            return;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (loaded)
            {
                return;
            }

            await using var stream = await httpClient.GetStreamAsync(DataUrl, cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var maps = BuildMaps(json.RootElement);
            items = maps.Items;
            champions = maps.Champions;
            traits = maps.Traits;
            loaded = true;

            logger.LogInformation(
                "Loaded TFT static data: {Items} items, {Champions} champions, {Traits} traits.",
                items.Count, champions.Count, traits.Count);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            // Non-fatal: names fall back to a cleaned identifier until a later retry.
            logger.LogWarning(ex, "Failed to load TFT static data; using id-cleanup fallback.");
        }
        finally
        {
            gate.Release();
        }
    }

    public string Item(string apiName) => Resolve(items, apiName);

    public string Champion(string apiName) => Resolve(champions, apiName);

    public string Trait(string apiName) => Resolve(traits, apiName);

    private static string Resolve(IReadOnlyDictionary<string, string> map, string apiName)
    {
        if (string.IsNullOrWhiteSpace(apiName))
        {
            return string.Empty;
        }

        return map.TryGetValue(apiName.Trim(), out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : Clean(apiName);
    }

    /// <summary>
    /// Builds apiName→display-name maps from the Community Dragon root element.
    /// Item names live at the top level; champions and traits live under each set.
    /// </summary>
    internal static (
        IReadOnlyDictionary<string, string> Items,
        IReadOnlyDictionary<string, string> Champions,
        IReadOnlyDictionary<string, string> Traits) BuildMaps(JsonElement root)
    {
        var items = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var champions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var traits = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty("items", out var itemArray) && itemArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in itemArray.EnumerateArray())
            {
                AddPair(items, item);
            }
        }

        if (root.TryGetProperty("setData", out var setData) && setData.ValueKind == JsonValueKind.Array)
        {
            foreach (var set in setData.EnumerateArray())
            {
                if (set.TryGetProperty("champions", out var champs) && champs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var champion in champs.EnumerateArray())
                    {
                        AddPair(champions, champion);
                    }
                }

                if (set.TryGetProperty("traits", out var traitArray) && traitArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var trait in traitArray.EnumerateArray())
                    {
                        AddPair(traits, trait);
                    }
                }
            }
        }

        return (items, champions, traits);
    }

    private static void AddPair(Dictionary<string, string> map, JsonElement element)
    {
        var apiName = GetString(element, "apiName");
        var name = GetString(element, "name");
        if (!string.IsNullOrWhiteSpace(apiName) && !string.IsNullOrWhiteSpace(name))
        {
            map[apiName.Trim()] = name.Trim();
        }
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    // Best-effort fallback when an id is missing from static data: strip the set/item
    // prefix and split camelCase so "TFT_Item_GiantSlayer" reads "Giant Slayer".
    internal static string Clean(string raw)
    {
        var stripped = raw
            .Replace("TFT_Item_", "", StringComparison.OrdinalIgnoreCase)
            .Replace("TFT_", "", StringComparison.OrdinalIgnoreCase);
        stripped = SetPrefixRegex().Replace(stripped, "");
        stripped = stripped.Replace('_', ' ').Trim();
        return CamelBoundaryRegex().Replace(stripped, " ");
    }

    [GeneratedRegex(@"^TFT\d+[_\s]?", RegexOptions.IgnoreCase)]
    private static partial Regex SetPrefixRegex();

    [GeneratedRegex(@"(?<=[a-z0-9])(?=[A-Z])")]
    private static partial Regex CamelBoundaryRegex();
}

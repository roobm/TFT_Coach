using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TFTCoach.Application.Interfaces;
using TFTCoach.Domain.Entities;
using TFTCoach.Domain.Interfaces;

namespace TFTCoach.Infrastructure.Meta;

/// <summary>
/// Tunables for <see cref="RiotMetaProvider"/>. Each Riot request is throttled to
/// the dev-key limit (100 req / 2 min), so larger samples don't fail — they just
/// take proportionally longer to warm up. Cost ≈ 1 + MaxPlayers (match-id lists) +
/// the number of *unique* matches (Challenger lobbies overlap heavily, so this
/// grows sub-linearly), and every match yields 8 board samples. Override at
/// runtime with META_MAX_PLAYERS / META_MATCHES_PER_PLAYER / META_MIN_SAMPLE /
/// META_TOP_COMPS.
/// </summary>
public sealed class RiotMetaOptions
{
    /// <summary>How many top Challenger players to sample.</summary>
    public int MaxPlayers { get; init; } = 25;

    /// <summary>How many recent ranked matches to pull per sampled player.</summary>
    public int MatchesPerPlayer { get; init; } = 8;

    /// <summary>Minimum times a comp must appear before it is surfaced.</summary>
    public int MinSampleSize { get; init; } = 4;

    /// <summary>How many comps the resulting snapshot contains.</summary>
    public int TopComps { get; init; } = 15;

    /// <summary>How long a computed snapshot stays fresh before a background refresh.</summary>
    public TimeSpan CacheTtl { get; init; } = TimeSpan.FromHours(6);
}

/// <summary>
/// Computes the live TFT meta from the top of the Riot ranked ladder (ToS-clean,
/// no third-party scraping): it samples Challenger players, pulls their recent
/// ranked matches, and aggregates the best-placing trait + carry combinations into
/// a <see cref="MetaSnapshot"/>.
///
/// The computation is expensive (many rate-limited Riot calls), so results are
/// cached and refreshed in the background. Callers never block on a fetch: a stale
/// or yet-to-be-computed snapshot falls back to <paramref name="fallback"/> (the
/// curated file), and the live snapshot is served once a warm-up completes.
/// </summary>
public sealed class RiotMetaProvider : IMetaProvider
{
    private static readonly Regex VersionRegex = new(@"(\d+)\.(\d+)", RegexOptions.Compiled);
    private static readonly Regex SetPrefixRegex = new(@"^TFT\d+[_\s]?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CamelBoundaryRegex = new(@"(?<=[a-z0-9])(?=[A-Z])", RegexOptions.Compiled);
    private const int RankedTftQueueId = 1100;

    private readonly IRiotLeagueClient leagueClient;
    private readonly IRiotMatchClient matchClient;
    private readonly IMetaProvider fallback;
    private readonly IMetaCacheRepository? cache;
    private readonly ITftStaticData? staticData;
    private readonly ILogger<RiotMetaProvider> logger;
    private readonly RiotMetaOptions options;
    private readonly SemaphoreSlim refreshGate = new(1, 1);

    private volatile MetaSnapshot? cached;
    private DateTimeOffset cachedAt = DateTimeOffset.MinValue;
    private Task? inFlight;
    private int dbHydrated;

    public RiotMetaProvider(
        IRiotLeagueClient leagueClient,
        IRiotMatchClient matchClient,
        IMetaProvider fallback,
        ILogger<RiotMetaProvider> logger,
        RiotMetaOptions? options = null,
        IMetaCacheRepository? cache = null,
        ITftStaticData? staticData = null)
    {
        this.leagueClient = leagueClient;
        this.matchClient = matchClient;
        this.fallback = fallback;
        this.cache = cache;
        this.staticData = staticData;
        this.logger = logger;
        this.options = options ?? new RiotMetaOptions();
    }

    public async Task<MetaSnapshot?> GetCurrentMetaAsync(CancellationToken cancellationToken = default)
    {
        // Hydrate the in-memory cache from the DB once, so a restart serves the last
        // persisted meta instantly instead of recomputing from the ladder.
        await EnsureHydratedFromDbAsync(cancellationToken);

        var snapshot = cached;
        var isFresh = snapshot is not null && DateTimeOffset.UtcNow - cachedAt < options.CacheTtl;
        if (isFresh)
        {
            return snapshot;
        }

        // Kick a background refresh (only one at a time) and never block the caller.
        TriggerRefresh();

        // Serve the last good live snapshot if we have one, else the curated file.
        if (snapshot is not null)
        {
            return snapshot;
        }

        return await fallback.GetCurrentMetaAsync(cancellationToken);
    }

    private async Task EnsureHydratedFromDbAsync(CancellationToken cancellationToken)
    {
        if (cache is null || Interlocked.CompareExchange(ref dbHydrated, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var persisted = await cache.GetLatestAsync(cancellationToken);
            if (persisted is not null && cached is null)
            {
                cached = persisted.Snapshot;
                cachedAt = persisted.ComputedAt;
                logger.LogInformation(
                    "Loaded cached meta from DB: {Set} patch {Patch} ({Count} comps), computed {Age:0.0}h ago.",
                    persisted.Snapshot.Set, persisted.Snapshot.Patch, persisted.Snapshot.Comps.Count,
                    (DateTimeOffset.UtcNow - persisted.ComputedAt).TotalHours);
            }
        }
        catch (Exception ex)
        {
            // A cache miss must never block live coaching; fall through to compute.
            logger.LogWarning(ex, "Failed to hydrate meta from DB cache; will compute live.");
        }
    }

    private void TriggerRefresh()
    {
        if (inFlight is { IsCompleted: false })
        {
            return;
        }

        lock (refreshGate)
        {
            if (inFlight is { IsCompleted: false })
            {
                return;
            }

            inFlight = Task.Run(RefreshAsync);
        }
    }

    private async Task RefreshAsync()
    {
        await refreshGate.WaitAsync();
        try
        {
            // Recheck freshness in case another refresh just finished.
            if (cached is not null && DateTimeOffset.UtcNow - cachedAt < options.CacheTtl)
            {
                return;
            }

            var snapshot = await ComputeAsync(CancellationToken.None);
            if (snapshot is not null && snapshot.Comps.Count > 0)
            {
                cached = snapshot;
                cachedAt = DateTimeOffset.UtcNow;
                logger.LogInformation(
                    "Computed live meta from Riot ladder: {Count} comps for {Set} patch {Patch}.",
                    snapshot.Comps.Count, snapshot.Set, snapshot.Patch);

                await PersistAsync(snapshot);
            }
            else
            {
                logger.LogWarning("Live meta computation produced no comps; keeping fallback/cached meta.");
            }
        }
        catch (Exception ex)
        {
            // Never let a background failure crash the host; coaching falls back gracefully.
            logger.LogError(ex, "Failed to compute live meta from the Riot ladder.");
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private async Task PersistAsync(MetaSnapshot snapshot)
    {
        if (cache is null)
        {
            return;
        }

        try
        {
            await cache.SaveAsync(snapshot, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Persisting is best-effort; a failed write must not lose the in-memory meta.
            logger.LogWarning(ex, "Failed to persist computed meta to DB cache.");
        }
    }

    private async Task<MetaSnapshot?> ComputeAsync(CancellationToken cancellationToken)
    {
        var puuids = await leagueClient.GetChallengerPuuidsAsync(options.MaxPlayers, cancellationToken);
        if (puuids.Count == 0)
        {
            logger.LogWarning("Challenger ladder returned no players; cannot compute live meta.");
            return null;
        }

        // Load static data up front so comp/carry/item names resolve to real display
        // names instead of raw Riot identifiers.
        if (staticData is not null)
        {
            await staticData.EnsureLoadedAsync(cancellationToken);
        }

        var results = new List<TftMatchResult>();
        var seenMatchIds = new HashSet<string>();

        foreach (var puuid in puuids)
        {
            var matchIds = await matchClient.GetRecentMatchIdsAsync(puuid, options.MatchesPerPlayer, cancellationToken);
            foreach (var matchId in matchIds)
            {
                if (!seenMatchIds.Add(matchId))
                {
                    continue;
                }

                // Aggregate every board in the lobby (placements 1-8), not just the sampled
                // player's, so each comp's average placement reflects how it actually does.
                var participants = await matchClient.GetMatchParticipantsAsync(matchId, cancellationToken);
                results.AddRange(participants.Where(p => p.QueueId == RankedTftQueueId));
            }
        }

        if (results.Count == 0)
        {
            return null;
        }

        // Only aggregate the most-played set in the sample so we never mix seasons.
        var dominantSet = results
            .GroupBy(r => r.SetNumber)
            .OrderByDescending(g => g.Count())
            .First().Key;
        var setResults = results.Where(r => r.SetNumber == dominantSet).ToList();

        var patch = ResolvePatch(setResults);
        var comps = AggregateComps(setResults);
        if (comps.Count == 0)
        {
            return null;
        }

        return new MetaSnapshot
        {
            Set = $"Set {dominantSet}",
            Patch = patch,
            Comps = comps,
        };
    }

    private List<MetaComp> AggregateComps(IReadOnlyList<TftMatchResult> results)
    {
        var groups = new Dictionary<string, CompAccumulator>();

        foreach (var result in results)
        {
            if (result.Placement <= 0)
            {
                continue;
            }

            var carry = SelectCarry(result.Units);
            var topTrait = SelectTopTrait(result.Traits);
            if (carry is null && topTrait is null)
            {
                continue;
            }

            var carryName = carry is null ? "Flex" : ResolveChampion(carry.Name);
            var traitName = topTrait is null ? "" : ResolveTrait(topTrait);
            var key = $"{traitName}|{carryName}";

            if (!groups.TryGetValue(key, out var acc))
            {
                acc = new CompAccumulator(traitName, carryName);
                groups[key] = acc;
            }

            acc.Add(result.Placement);
            if (carry is not null)
            {
                foreach (var item in carry.Items)
                {
                    acc.Items[item] = acc.Items.GetValueOrDefault(item) + 1;
                }
            }
        }

        return groups.Values
            .Where(g => g.Count >= options.MinSampleSize)
            .OrderBy(g => g.AveragePlacement)
            .Take(options.TopComps)
            .Select(ToMetaComp)
            .ToList();
    }

    private MetaComp ToMetaComp(CompAccumulator acc)
    {
        var name = string.IsNullOrEmpty(acc.Trait) ? acc.Carry : $"{acc.Trait} {acc.Carry}";
        var keyItems = acc.Items
            .OrderByDescending(kv => kv.Value)
            .Take(3)
            .Select(kv => ResolveItem(kv.Key))
            .ToList();

        return new MetaComp
        {
            Name = name,
            Tier = TierFor(acc.AveragePlacement),
            Playstyle = string.Format(
                CultureInfo.InvariantCulture,
                "KR Challenger sample · avg place {0:0.0} over {1} games",
                acc.AveragePlacement, acc.Count),
            Carries = acc.Carry == "Flex" ? [] : [acc.Carry],
            KeyItems = keyItems,
            Notes = $"Top item{(keyItems.Count == 1 ? "" : "s")} on {acc.Carry}: {(keyItems.Count == 0 ? "varies" : string.Join(", ", keyItems))}.",
        };
    }

    private static UnitDetail? SelectCarry(IReadOnlyList<UnitDetail> units)
    {
        if (units.Count == 0)
        {
            return null;
        }

        // The carry is the unit holding the most completed items, breaking ties by
        // star level then name so the result is deterministic.
        return units
            .OrderByDescending(u => u.Items.Count)
            .ThenByDescending(u => u.Tier)
            .ThenBy(u => u.Name, StringComparer.Ordinal)
            .First();
    }

    private static string? SelectTopTrait(IReadOnlyList<string> traits)
    {
        // Traits arrive highest-tier first as "TFTxx_Name tier"; take the leading name.
        var raw = traits.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var space = raw.LastIndexOf(' ');
        return space > 0 ? raw[..space] : raw;
    }

    private string ResolvePatch(IReadOnlyList<TftMatchResult> results)
    {
        var version = results
            .Select(r => r.GameVersion)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        if (version is not null)
        {
            var match = VersionRegex.Match(version);
            if (match.Success)
            {
                return $"{match.Groups[1].Value}.{match.Groups[2].Value}";
            }
        }

        return "unknown";
    }

    private static string TierFor(double averagePlacement) => averagePlacement switch
    {
        <= 3.6 => "S",
        <= 4.2 => "A",
        <= 4.8 => "B",
        _ => "C",
    };

    // Resolve raw Riot identifiers to in-game display names via static data, falling
    // back to a best-effort cleanup when static data is unavailable.
    private string ResolveChampion(string apiName) => staticData?.Champion(apiName) ?? CleanName(apiName);

    private string ResolveTrait(string apiName) => staticData?.Trait(apiName) ?? CleanName(apiName);

    private string ResolveItem(string apiName) => staticData?.Item(apiName) ?? CleanItemName(apiName);

    private static string CleanName(string raw)
    {
        var stripped = SetPrefixRegex.Replace(raw, "");
        stripped = stripped.Replace('_', ' ').Trim();
        return CamelBoundaryRegex.Replace(stripped, " ");
    }

    private static string CleanItemName(string raw)
    {
        var stripped = raw
            .Replace("TFT_Item_", "", StringComparison.OrdinalIgnoreCase)
            .Replace("TFT_", "", StringComparison.OrdinalIgnoreCase);
        stripped = SetPrefixRegex.Replace(stripped, "");
        stripped = stripped.Replace('_', ' ').Trim();
        return CamelBoundaryRegex.Replace(stripped, " ");
    }

    private sealed class CompAccumulator(string trait, string carry)
    {
        public string Trait { get; } = trait;
        public string Carry { get; } = carry;
        public int Count { get; private set; }
        public Dictionary<string, int> Items { get; } = new(StringComparer.Ordinal);

        private int placementSum;

        public double AveragePlacement => Count == 0 ? 8 : (double)placementSum / Count;

        public void Add(int placement)
        {
            placementSum += placement;
            Count++;
        }
    }
}

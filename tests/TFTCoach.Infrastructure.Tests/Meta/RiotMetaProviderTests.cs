using Microsoft.Extensions.Logging.Abstractions;
using TFTCoach.Application.Interfaces;
using TFTCoach.Domain.Entities;
using TFTCoach.Domain.Interfaces;
using TFTCoach.Infrastructure.Meta;

namespace TFTCoach.Infrastructure.Tests.Meta;

public class RiotMetaProviderTests
{
    [Fact]
    public async Task GetCurrentMetaAsync_ShouldAggregateLadderIntoComps_WhenMatchesRepeat()
    {
        var league = new FakeLeagueClient(["p1"]);
        var match = new FakeMatchClient(new Dictionary<string, IReadOnlyList<string>>
        {
            ["p1"] = ["m1", "m2"],
        }, new Dictionary<string, TftMatchResult>
        {
            ["m1"] = CypherAphelios("m1", placement: 1),
            ["m2"] = CypherAphelios("m2", placement: 2),
        });
        var provider = new RiotMetaProvider(
            league, match, new FakeMetaProvider(null), NullLogger<RiotMetaProvider>.Instance,
            new RiotMetaOptions { MaxPlayers = 1, MatchesPerPlayer = 2, MinSampleSize = 2, TopComps = 6 });

        var snapshot = await WaitForLiveMetaAsync(provider);

        Assert.NotNull(snapshot);
        Assert.Equal("Set 14", snapshot!.Set);
        Assert.Equal("14.6", snapshot.Patch);
        var comp = Assert.Single(snapshot.Comps);
        Assert.Equal("Cypher Aphelios", comp.Name);
        Assert.Equal("S", comp.Tier);
        Assert.Contains("Aphelios", comp.Carries);
        Assert.Contains("Guinsoos Rageblade", comp.KeyItems);
    }

    [Fact]
    public async Task GetCurrentMetaAsync_ShouldSpanTiers_WhenLobbyHasVariedPlacements()
    {
        // One lobby: a strong comp that wins twice and a weak comp that bottoms twice.
        var league = new FakeLeagueClient(["p1"]);
        var match = new FakeMatchClient(
            new Dictionary<string, IReadOnlyList<string>> { ["p1"] = ["m1", "m2"] },
            new Dictionary<string, TftMatchResult>(),
            new Dictionary<string, IReadOnlyList<TftMatchResult>>
            {
                ["m1"] = [CypherAphelios("m1", placement: 1), JinxReroll("m1", placement: 7)],
                ["m2"] = [CypherAphelios("m2", placement: 2), JinxReroll("m2", placement: 8)],
            });
        var provider = new RiotMetaProvider(
            league, match, new FakeMetaProvider(null), NullLogger<RiotMetaProvider>.Instance,
            new RiotMetaOptions { MaxPlayers = 1, MatchesPerPlayer = 2, MinSampleSize = 2, TopComps = 6 });

        var snapshot = await WaitForLiveMetaAsync(provider);

        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot!.Comps.Count);
        var tiers = snapshot.Comps.Select(c => c.Tier).ToHashSet();
        Assert.Contains("S", tiers);
        Assert.Contains("C", tiers);
    }

    [Fact]
    public async Task GetCurrentMetaAsync_ShouldReturnFallback_WhenLadderEmpty()
    {
        var fallback = new MetaSnapshot
        {
            Set = "Set 14",
            Patch = "14.6",
            Comps = [new MetaComp { Name = "Curated Comp" }],
        };
        var provider = new RiotMetaProvider(
            new FakeLeagueClient([]), new FakeMatchClient(new(), new()),
            new FakeMetaProvider(fallback), NullLogger<RiotMetaProvider>.Instance,
            new RiotMetaOptions { MaxPlayers = 1 });

        var snapshot = await provider.GetCurrentMetaAsync();

        Assert.Same(fallback, snapshot);
    }

    [Fact]
    public async Task GetCurrentMetaAsync_ShouldPersistComputedMeta_WhenCacheProvided()
    {
        var league = new FakeLeagueClient(["p1"]);
        var match = new FakeMatchClient(new Dictionary<string, IReadOnlyList<string>>
        {
            ["p1"] = ["m1", "m2"],
        }, new Dictionary<string, TftMatchResult>
        {
            ["m1"] = CypherAphelios("m1", placement: 1),
            ["m2"] = CypherAphelios("m2", placement: 2),
        });
        var cache = new FakeMetaCache();
        var provider = new RiotMetaProvider(
            league, match, new FakeMetaProvider(null), NullLogger<RiotMetaProvider>.Instance,
            new RiotMetaOptions { MaxPlayers = 1, MatchesPerPlayer = 2, MinSampleSize = 2, TopComps = 6 }, cache);

        await WaitForLiveMetaAsync(provider);

        Assert.NotNull(cache.Saved);
        Assert.Equal("Set 14", cache.Saved!.Set);
        Assert.Equal("14.6", cache.Saved.Patch);
        Assert.NotEmpty(cache.Saved.Comps);
    }

    [Fact]
    public async Task GetCurrentMetaAsync_ShouldServeDbCache_WhenFresh()
    {
        // A fresh DB snapshot should be served immediately without recomputing.
        var persisted = new MetaSnapshot
        {
            Set = "Set 99",
            Patch = "99.9",
            Comps = [new MetaComp { Name = "Persisted Comp", Tier = "S" }],
        };
        var cache = new FakeMetaCache
        {
            Latest = new CachedMeta { Snapshot = persisted, ComputedAt = DateTimeOffset.UtcNow },
        };
        var provider = new RiotMetaProvider(
            new FakeLeagueClient([]), new FakeMatchClient(new(), new()),
            new FakeMetaProvider(null), NullLogger<RiotMetaProvider>.Instance,
            new RiotMetaOptions { MaxPlayers = 1, CacheTtl = TimeSpan.FromHours(6) }, cache);

        var snapshot = await provider.GetCurrentMetaAsync();

        Assert.Same(persisted, snapshot);
    }

    private static async Task<MetaSnapshot?> WaitForLiveMetaAsync(RiotMetaProvider provider)
    {
        // The provider refreshes in the background, so the first call returns the
        // fallback; poll briefly until the computed live snapshot is served.
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var snapshot = await provider.GetCurrentMetaAsync();
            if (snapshot is not null)
            {
                return snapshot;
            }

            await Task.Delay(20);
        }

        return null;
    }

    private static TftMatchResult CypherAphelios(string matchId, int placement) => new()
    {
        MatchId = matchId,
        QueueId = 1100,
        SetNumber = 14,
        GameVersion = "Version 14.6.123",
        Placement = placement,
        Traits = ["TFT14_Cypher 3", "TFT14_Marksman 2"],
        Units =
        [
            new UnitDetail
            {
                Name = "TFT14_Aphelios",
                Tier = 2,
                Items = ["TFT_Item_GuinsoosRageblade", "TFT_Item_LastWhisper", "TFT_Item_InfinityEdge"],
            },
            new UnitDetail { Name = "TFT14_Leona", Tier = 2, Items = [] },
        ],
    };

    private static TftMatchResult JinxReroll(string matchId, int placement) => new()
    {
        MatchId = matchId,
        QueueId = 1100,
        SetNumber = 14,
        GameVersion = "Version 14.6.123",
        Placement = placement,
        Traits = ["TFT14_Exotech 4", "TFT14_Marksman 2"],
        Units =
        [
            new UnitDetail
            {
                Name = "TFT14_Jinx",
                Tier = 3,
                Items = ["TFT_Item_InfinityEdge", "TFT_Item_LastWhisper", "TFT_Item_GiantSlayer"],
            },
            new UnitDetail { Name = "TFT14_Zeri", Tier = 1, Items = [] },
        ],
    };

    private sealed class FakeLeagueClient(IReadOnlyList<string> puuids) : IRiotLeagueClient
    {
        public Task<IReadOnlyList<RankedEntry>> GetRankedEntriesAsync(string puuid, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RankedEntry>>([]);

        public Task<IReadOnlyList<string>> GetChallengerPuuidsAsync(int maxPlayers, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(puuids.Take(maxPlayers).ToList());
    }

    private sealed class FakeMatchClient(
        Dictionary<string, IReadOnlyList<string>> matchIdsByPuuid,
        Dictionary<string, TftMatchResult> matchesById,
        Dictionary<string, IReadOnlyList<TftMatchResult>>? participantsById = null) : IRiotMatchClient
    {
        public Task<string> ResolvePuuidAsync(string gameName, string tagLine, CancellationToken cancellationToken = default)
            => Task.FromResult("p1");

        public Task<IReadOnlyList<string>> GetRecentMatchIdsAsync(string puuid, int count, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(
                matchIdsByPuuid.TryGetValue(puuid, out var ids) ? ids.Take(count).ToList() : []);

        public Task<TftMatchResult?> GetMatchAsync(string matchId, string puuid, CancellationToken cancellationToken = default)
            => Task.FromResult(matchesById.GetValueOrDefault(matchId));

        public Task<IReadOnlyList<TftMatchResult>> GetMatchParticipantsAsync(string matchId, CancellationToken cancellationToken = default)
        {
            if (participantsById is not null && participantsById.TryGetValue(matchId, out var participants))
            {
                return Task.FromResult(participants);
            }

            return Task.FromResult<IReadOnlyList<TftMatchResult>>(
                matchesById.TryGetValue(matchId, out var match) ? [match] : []);
        }
    }

    private sealed class FakeMetaProvider(MetaSnapshot? snapshot) : IMetaProvider
    {
        public Task<MetaSnapshot?> GetCurrentMetaAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(snapshot);
    }

    private sealed class FakeMetaCache : IMetaCacheRepository
    {
        public MetaSnapshot? Saved { get; private set; }
        public CachedMeta? Latest { get; init; }

        public Task SaveAsync(MetaSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Saved = snapshot;
            return Task.CompletedTask;
        }

        public Task<CachedMeta?> GetLatestAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Latest);
    }
}

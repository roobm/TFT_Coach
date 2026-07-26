using Microsoft.Extensions.Logging.Abstractions;
using TFTCoach.Application.Common;
using TFTCoach.Application.Interfaces;
using TFTCoach.Application.UseCases.GetMatchHistory;
using TFTCoach.Domain.Entities;
using TFTCoach.Domain.Interfaces;

namespace TFTCoach.Application.Tests.UseCases.GetMatchHistory;

public class GetMatchHistoryUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldResolvePuuidAndReturnMatches_WhenRiotIdIsValid()
    {
        // Arrange
        var client = new FakeRiotMatchClient("puuid-123", ["EUW_1"]);
        var useCase = NewUseCase(client, new RecordingMatchHistoryRepository());

        // Act
        var result = await useCase.ExecuteAsync(new GetMatchHistoryRequest
        {
            GameName = "Faker",
            TagLine = "EUW",
            Count = 5,
        });
        var response = AssertSuccess(result);

        // Assert
        Assert.Equal("puuid-123", response.Puuid);
        Assert.Single(response.Matches);
        Assert.Equal("Faker", client.LastGameName);
        Assert.Equal("EUW", client.LastTagLine);
        Assert.Equal(5, client.LastCount);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFoundError_WhenPuuidResolutionFails()
    {
        // Arrange
        var client = new FakeRiotMatchClient("puuid", ["EUW_1"], throwOnResolve: true);
        var useCase = NewUseCase(client, new RecordingMatchHistoryRepository());

        // Act
        var result = await useCase.ExecuteAsync(new GetMatchHistoryRequest
        {
            GameName = "Missing",
            TagLine = "EUW",
            Count = 5,
        });

        // Assert
        Assert.False(result.Success);
        Assert.Equal("not_found.player", result.Error?.Code);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(50, 50)]
    [InlineData(500, 200)]
    public async Task ExecuteAsync_ShouldClampCount_WhenOutOfRange(int requested, int expected)
    {
        // Arrange
        var client = new FakeRiotMatchClient("puuid", []);
        var useCase = NewUseCase(client, new RecordingMatchHistoryRepository());

        // Act
        var result = await useCase.ExecuteAsync(new GetMatchHistoryRequest
        {
            GameName = "Name",
            TagLine = "TAG",
            Count = requested,
        });
        AssertSuccess(result);

        // Assert
        Assert.Equal(expected, client.LastCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_ShouldRequestAllMatches_WhenCountIsZeroOrNegative(int requested)
    {
        // Arrange
        var client = new FakeRiotMatchClient("puuid", []);
        var useCase = NewUseCase(client, new RecordingMatchHistoryRepository());

        // Act
        var result = await useCase.ExecuteAsync(new GetMatchHistoryRequest
        {
            GameName = "Name",
            TagLine = "TAG",
            Count = requested,
        });
        AssertSuccess(result);

        // Assert
        Assert.Equal(int.MaxValue, client.LastCount);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldServeCachedMatchesWithoutCallingRiot_WhenAlreadyStored()
    {
        // Arrange — both ids are already in the cache, so no detail call should be made.
        var client = new FakeRiotMatchClient("puuid", ["M1", "M2"]);
        var repo = new RecordingMatchHistoryRepository();
        repo.Seed(new TftMatchResult { MatchId = "M1", Placement = 1, SetNumber = 15 });
        repo.Seed(new TftMatchResult { MatchId = "M2", Placement = 4, SetNumber = 15 });
        var useCase = NewUseCase(client, repo);

        // Act
        var result = await useCase.ExecuteAsync(new GetMatchHistoryRequest
        {
            GameName = "Name",
            TagLine = "TAG",
            Count = 2,
        });
        var response = AssertSuccess(result);

        // Assert
        Assert.Equal(2, response.Matches.Count);
        Assert.Empty(client.FetchedMatchIds);
        Assert.Null(repo.SavedPuuid);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldOnlyFetchAndCacheMissingMatches_WhenSomeAreStored()
    {
        // Arrange — M1 cached, M2 missing → only M2 is fetched and persisted.
        var client = new FakeRiotMatchClient("puuid", ["M1", "M2"]);
        var repo = new RecordingMatchHistoryRepository();
        repo.Seed(new TftMatchResult { MatchId = "M1", Placement = 1, SetNumber = 15 });
        var useCase = NewUseCase(client, repo);

        // Act
        var result = await useCase.ExecuteAsync(new GetMatchHistoryRequest
        {
            GameName = "Name",
            TagLine = "TAG",
            Count = 2,
        });
        var response = AssertSuccess(result);

        // Assert
        Assert.Equal(2, response.Matches.Count);
        Assert.Equal(["M2"], client.FetchedMatchIds);
        Assert.Equal("puuid", repo.SavedPuuid);
        Assert.Equal("M2", Assert.Single(repo.SavedMatches).MatchId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPreserveRiotOrder_WhenMergingCacheAndFetched()
    {
        // Arrange — newest-first order from Riot must be preserved across cache + fetch.
        var client = new FakeRiotMatchClient("puuid", ["M1", "M2", "M3"]);
        var repo = new RecordingMatchHistoryRepository();
        repo.Seed(new TftMatchResult { MatchId = "M2", Placement = 4 });
        var useCase = NewUseCase(client, repo);

        // Act
        var result = await useCase.ExecuteAsync(new GetMatchHistoryRequest
        {
            GameName = "Name",
            TagLine = "TAG",
            Count = 3,
        });
        var response = AssertSuccess(result);

        // Assert
        Assert.Equal(["M1", "M2", "M3"], response.Matches.Select(m => m.MatchId).ToList());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStillFetchFromRiot_WhenCacheReadThrows()
    {
        // Arrange — a failing cache must degrade gracefully to a full Riot fetch.
        var client = new FakeRiotMatchClient("puuid", ["M1"]);
        var useCase = NewUseCase(client, new ThrowingMatchHistoryRepository());

        // Act
        var result = await useCase.ExecuteAsync(new GetMatchHistoryRequest
        {
            GameName = "Name",
            TagLine = "TAG",
            Count = 1,
        });
        var response = AssertSuccess(result);

        // Assert
        Assert.Single(response.Matches);
        Assert.Equal(["M1"], client.FetchedMatchIds);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStopAtPreviousSet_WhenMatchesSpanMultipleSets()
    {
        // Arrange — M1/M2 are the current set (15); M3/M4 belong to an older set (14).
        var sets = new Dictionary<string, int> { ["M1"] = 15, ["M2"] = 15, ["M3"] = 14, ["M4"] = 14 };
        var client = new FakeRiotMatchClient("puuid", ["M1", "M2", "M3", "M4"], sets);
        var useCase = NewUseCase(client, new RecordingMatchHistoryRepository());

        // Act — request "all" games.
        var result = await useCase.ExecuteAsync(new GetMatchHistoryRequest
        {
            GameName = "Name",
            TagLine = "TAG",
            Count = 0,
        });
        var response = AssertSuccess(result);

        // Assert — only current-set matches are returned; the older set is excluded.
        Assert.Equal(["M1", "M2"], response.Matches.Select(m => m.MatchId).ToList());
        Assert.Equal(15, response.CurrentSet);
        Assert.Equal(15, response.Set);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnRequestedOlderSet_WhenSetSpecified()
    {
        // Arrange — M1/M2 current set (15), M3/M4 older (14), M5 older still (13).
        var sets = new Dictionary<string, int> { ["M1"] = 15, ["M2"] = 15, ["M3"] = 14, ["M4"] = 14, ["M5"] = 13 };
        var client = new FakeRiotMatchClient("puuid", ["M1", "M2", "M3", "M4", "M5"], sets);
        var useCase = NewUseCase(client, new RecordingMatchHistoryRepository());

        // Act — explicitly request set 14.
        var result = await useCase.ExecuteAsync(new GetMatchHistoryRequest
        {
            GameName = "Name",
            TagLine = "TAG",
            Count = 0,
            Set = 14,
        });
        var response = AssertSuccess(result);

        // Assert — newer set (15) skipped, older set (13) stops the walk.
        Assert.Equal(["M3", "M4"], response.Matches.Select(m => m.MatchId).ToList());
        Assert.Equal(14, response.Set);
        Assert.Equal(15, response.CurrentSet);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAllSets_WhenSetIsNegative()
    {
        // Arrange — Set < 0 requests all seasons.
        var sets = new Dictionary<string, int> { ["M1"] = 15, ["M2"] = 15, ["M3"] = 14, ["M4"] = 13 };
        var client = new FakeRiotMatchClient("puuid", ["M1", "M2", "M3", "M4"], sets);
        var useCase = NewUseCase(client, new RecordingMatchHistoryRepository());

        // Act
        var result = await useCase.ExecuteAsync(new GetMatchHistoryRequest
        {
            GameName = "Name",
            TagLine = "TAG",
            Count = 0,
            Set = -1,
        });
        var response = AssertSuccess(result);

        // Assert
        Assert.Equal(["M1", "M2", "M3", "M4"], response.Matches.Select(m => m.MatchId).ToList());
        Assert.Equal(int.MaxValue, client.LastCount);
        Assert.Equal(-1, response.Set);
    }

    private static GetMatchHistoryUseCase NewUseCase(IRiotMatchClient client, IMatchHistoryRepository repo)
        => new(client, repo, NullLogger<GetMatchHistoryUseCase>.Instance);

    private static T AssertSuccess<T>(RequestResult<T> result)
    {
        Assert.True(result.Success);
        Assert.Null(result.Error);
        return Assert.IsType<T>(result.Data);
    }

    private sealed class FakeRiotMatchClient(
        string puuid,
        IReadOnlyList<string> matchIds,
        IReadOnlyDictionary<string, int>? setByMatchId = null,
        bool throwOnResolve = false) : IRiotMatchClient
    {
        public string? LastGameName { get; private set; }
        public string? LastTagLine { get; private set; }
        public int LastCount { get; private set; }
        public List<string> FetchedMatchIds { get; } = [];

        public Task<string> ResolvePuuidAsync(string gameName, string tagLine, CancellationToken cancellationToken = default)
        {
            LastGameName = gameName;
            LastTagLine = tagLine;
            if (throwOnResolve)
            {
                throw new KeyNotFoundException("Summoner not found.");
            }
            return Task.FromResult(puuid);
        }

        public Task<IReadOnlyList<string>> GetRecentMatchIdsAsync(string puuid, int count, CancellationToken cancellationToken = default)
        {
            LastCount = count;
            return Task.FromResult<IReadOnlyList<string>>(matchIds.Take(count).ToList());
        }

        public Task<TftMatchResult?> GetMatchAsync(string matchId, string puuid, CancellationToken cancellationToken = default)
        {
            FetchedMatchIds.Add(matchId);
            var set = setByMatchId is not null && setByMatchId.TryGetValue(matchId, out var s) ? s : 15;
            return Task.FromResult<TftMatchResult?>(new TftMatchResult { MatchId = matchId, Placement = 8, SetNumber = set });
        }

        public Task<IReadOnlyList<TftMatchResult>> GetMatchParticipantsAsync(string matchId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TftMatchResult>>([]);
    }

    private sealed class RecordingMatchHistoryRepository : IMatchHistoryRepository
    {
        private readonly Dictionary<string, TftMatchResult> _store = [];

        public string? SavedPuuid { get; private set; }
        public IReadOnlyList<TftMatchResult> SavedMatches { get; private set; } = [];

        public void Seed(TftMatchResult match) => _store[match.MatchId] = match;

        public Task SaveAsync(string puuid, IReadOnlyList<TftMatchResult> matches, CancellationToken cancellationToken = default)
        {
            SavedPuuid = puuid;
            SavedMatches = matches;
            foreach (var match in matches)
            {
                _store[match.MatchId] = match;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TftMatchResult>> GetByPuuidAsync(string puuid, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TftMatchResult>>(_store.Values.Take(limit).ToList());

        public Task<IReadOnlyList<TftMatchResult>> GetByMatchIdsAsync(string puuid, IReadOnlyList<string> matchIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TftMatchResult>>(
                matchIds.Where(_store.ContainsKey).Select(id => _store[id]).ToList());
    }

    private sealed class ThrowingMatchHistoryRepository : IMatchHistoryRepository
    {
        public Task SaveAsync(string puuid, IReadOnlyList<TftMatchResult> matches, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<TftMatchResult>> GetByPuuidAsync(string puuid, int limit, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("db down");

        public Task<IReadOnlyList<TftMatchResult>> GetByMatchIdsAsync(string puuid, IReadOnlyList<string> matchIds, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("db down");
    }
}

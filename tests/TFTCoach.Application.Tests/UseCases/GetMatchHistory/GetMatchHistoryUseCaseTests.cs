using Microsoft.Extensions.Logging.Abstractions;
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
        var response = await useCase.ExecuteAsync(new GetMatchHistoryRequest
        {
            GameName = "Faker",
            TagLine = "EUW",
            Count = 5,
        });

        // Assert
        Assert.Equal("puuid-123", response.Puuid);
        Assert.Single(response.Matches);
        Assert.Equal("Faker", client.LastGameName);
        Assert.Equal("EUW", client.LastTagLine);
        Assert.Equal(5, client.LastCount);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(50, 50)]
    [InlineData(500, 200)]
    public async Task ExecuteAsync_ShouldClampCount_WhenOutOfRange(int requested, int expected)
    {
        // Arrange
        var client = new FakeRiotMatchClient("puuid", []);
        var useCase = NewUseCase(client, new RecordingMatchHistoryRepository());

        // Act
        await useCase.ExecuteAsync(new GetMatchHistoryRequest
        {
            GameName = "Name",
            TagLine = "TAG",
            Count = requested,
        });

        // Assert
        Assert.Equal(expected, client.LastCount);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldServeCachedMatchesWithoutCallingRiot_WhenAlreadyStored()
    {
        // Arrange — both ids are already in the cache, so no detail call should be made.
        var client = new FakeRiotMatchClient("puuid", ["M1", "M2"]);
        var repo = new RecordingMatchHistoryRepository();
        repo.Seed(new TftMatchResult { MatchId = "M1", Placement = 1 });
        repo.Seed(new TftMatchResult { MatchId = "M2", Placement = 4 });
        var useCase = NewUseCase(client, repo);

        // Act
        var response = await useCase.ExecuteAsync(new GetMatchHistoryRequest
        {
            GameName = "Name",
            TagLine = "TAG",
            Count = 2,
        });

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
        repo.Seed(new TftMatchResult { MatchId = "M1", Placement = 1 });
        var useCase = NewUseCase(client, repo);

        // Act
        var response = await useCase.ExecuteAsync(new GetMatchHistoryRequest
        {
            GameName = "Name",
            TagLine = "TAG",
            Count = 2,
        });

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
        var response = await useCase.ExecuteAsync(new GetMatchHistoryRequest
        {
            GameName = "Name",
            TagLine = "TAG",
            Count = 3,
        });

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
        var response = await useCase.ExecuteAsync(new GetMatchHistoryRequest
        {
            GameName = "Name",
            TagLine = "TAG",
            Count = 1,
        });

        // Assert
        Assert.Single(response.Matches);
        Assert.Equal(["M1"], client.FetchedMatchIds);
    }

    private static GetMatchHistoryUseCase NewUseCase(IRiotMatchClient client, IMatchHistoryRepository repo)
        => new(client, repo, NullLogger<GetMatchHistoryUseCase>.Instance);

    private sealed class FakeRiotMatchClient(string puuid, IReadOnlyList<string> matchIds) : IRiotMatchClient
    {
        public string? LastGameName { get; private set; }
        public string? LastTagLine { get; private set; }
        public int LastCount { get; private set; }
        public List<string> FetchedMatchIds { get; } = [];

        public Task<string> ResolvePuuidAsync(string gameName, string tagLine, CancellationToken cancellationToken = default)
        {
            LastGameName = gameName;
            LastTagLine = tagLine;
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
            return Task.FromResult<TftMatchResult?>(new TftMatchResult { MatchId = matchId, Placement = 8 });
        }
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

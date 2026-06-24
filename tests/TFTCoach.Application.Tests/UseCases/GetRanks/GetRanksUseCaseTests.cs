using Microsoft.Extensions.Logging.Abstractions;
using TFTCoach.Application.Interfaces;
using TFTCoach.Application.UseCases.GetRanks;
using TFTCoach.Domain.Entities;
using TFTCoach.Domain.Interfaces;

namespace TFTCoach.Application.Tests.UseCases.GetRanks;

public class GetRanksUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldResolvePuuidAndReturnEntries_WhenRiotIdIsValid()
    {
        // Arrange
        var matchClient = new FakeRiotMatchClient("puuid-123");
        var leagueClient = new FakeRiotLeagueClient(
        [
            new RankedEntry { QueueType = "RANKED_TFT", Tier = "GOLD", Division = "II", LeaguePoints = 40, Wins = 12, Losses = 8 },
        ]);
        var useCase = NewUseCase(matchClient, leagueClient, new RecordingRankHistoryRepository());

        // Act
        var response = await useCase.ExecuteAsync(new GetRanksRequest
        {
            GameName = "Faker",
            TagLine = "EUW",
        });

        // Assert
        Assert.Equal("puuid-123", response.Puuid);
        var entry = Assert.Single(response.Entries);
        Assert.Equal("RANKED_TFT", entry.QueueType);
        Assert.Equal("GOLD", entry.Tier);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldQueryLeagueWithResolvedPuuid_WhenExecuted()
    {
        // Arrange
        var matchClient = new FakeRiotMatchClient("puuid-abc");
        var leagueClient = new FakeRiotLeagueClient([]);
        var useCase = NewUseCase(matchClient, leagueClient, new RecordingRankHistoryRepository());

        // Act
        await useCase.ExecuteAsync(new GetRanksRequest { GameName = "Name", TagLine = "TAG" });

        // Assert
        Assert.Equal("puuid-abc", leagueClient.RequestedPuuid);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyEntries_WhenPlayerIsUnranked()
    {
        // Arrange
        var useCase = NewUseCase(
            new FakeRiotMatchClient("puuid"),
            new FakeRiotLeagueClient([]),
            new RecordingRankHistoryRepository());

        // Act
        var response = await useCase.ExecuteAsync(new GetRanksRequest { GameName = "Name", TagLine = "TAG" });

        // Assert
        Assert.Empty(response.Entries);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRecordSnapshotsAndReturnHistory_WhenRanked()
    {
        // Arrange
        var entries = new[]
        {
            new RankedEntry { QueueType = "RANKED_TFT", Tier = "GOLD", Division = "II", LeaguePoints = 40, Wins = 12, Losses = 8 },
        };
        var repo = new RecordingRankHistoryRepository();
        var useCase = NewUseCase(new FakeRiotMatchClient("puuid"), new FakeRiotLeagueClient(entries), repo);

        // Act
        var response = await useCase.ExecuteAsync(new GetRanksRequest { GameName = "Name", TagLine = "TAG" });

        // Assert
        Assert.Equal("puuid", repo.RecordedPuuid);
        Assert.Single(repo.RecordedEntries);
        Assert.Single(response.History);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStillReturnEntries_WhenHistoryStoreThrows()
    {
        // Arrange — a failing history store must not fail the request.
        var entries = new[]
        {
            new RankedEntry { QueueType = "RANKED_TFT", Tier = "SILVER", Division = "I", LeaguePoints = 75 },
        };
        var useCase = NewUseCase(new FakeRiotMatchClient("puuid"), new FakeRiotLeagueClient(entries), new ThrowingRankHistoryRepository());

        // Act
        var response = await useCase.ExecuteAsync(new GetRanksRequest { GameName = "Name", TagLine = "TAG" });

        // Assert
        Assert.Single(response.Entries);
        Assert.Empty(response.History);
    }

    private static GetRanksUseCase NewUseCase(
        IRiotMatchClient matchClient,
        IRiotLeagueClient leagueClient,
        IRankHistoryRepository rankHistoryRepository)
        => new(matchClient, leagueClient, rankHistoryRepository, NullLogger<GetRanksUseCase>.Instance);

    private sealed class FakeRiotMatchClient(string puuid) : IRiotMatchClient
    {
        public Task<string> ResolvePuuidAsync(string gameName, string tagLine, CancellationToken cancellationToken = default)
            => Task.FromResult(puuid);

        public Task<IReadOnlyList<string>> GetRecentMatchIdsAsync(string puuid, int count, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<TftMatchResult?> GetMatchAsync(string matchId, string puuid, CancellationToken cancellationToken = default)
            => Task.FromResult<TftMatchResult?>(null);

        public Task<IReadOnlyList<TftMatchResult>> GetMatchParticipantsAsync(string matchId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TftMatchResult>>([]);
    }

    private sealed class FakeRiotLeagueClient(IReadOnlyList<RankedEntry> entries) : IRiotLeagueClient
    {
        public string? RequestedPuuid { get; private set; }

        public Task<IReadOnlyList<RankedEntry>> GetRankedEntriesAsync(string puuid, CancellationToken cancellationToken = default)
        {
            RequestedPuuid = puuid;
            return Task.FromResult(entries);
        }

        public Task<IReadOnlyList<string>> GetChallengerPuuidsAsync(int maxPlayers, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class RecordingRankHistoryRepository : IRankHistoryRepository
    {
        public string? RecordedPuuid { get; private set; }
        public IReadOnlyList<RankedEntry> RecordedEntries { get; private set; } = [];

        public Task RecordAsync(string puuid, IReadOnlyList<RankedEntry> entries, CancellationToken cancellationToken = default)
        {
            RecordedPuuid = puuid;
            RecordedEntries = entries;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RankSnapshot>> GetHistoryAsync(string puuid, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RankSnapshot>>(
                RecordedEntries
                    .Select(e => new RankSnapshot
                    {
                        QueueType = e.QueueType,
                        Tier = e.Tier,
                        Division = e.Division,
                        LeaguePoints = e.LeaguePoints,
                        Wins = e.Wins,
                        Losses = e.Losses,
                        RecordedAt = DateTimeOffset.UtcNow,
                    })
                    .ToList());
    }

    private sealed class ThrowingRankHistoryRepository : IRankHistoryRepository
    {
        public Task RecordAsync(string puuid, IReadOnlyList<RankedEntry> entries, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("record failed");

        public Task<IReadOnlyList<RankSnapshot>> GetHistoryAsync(string puuid, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("history read failed");
    }
}

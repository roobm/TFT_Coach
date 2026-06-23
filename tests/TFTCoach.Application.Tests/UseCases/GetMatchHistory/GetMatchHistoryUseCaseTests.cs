using Microsoft.Extensions.Logging.Abstractions;
using TFTCoach.Application.Interfaces;
using TFTCoach.Application.UseCases.GetMatchHistory;
using TFTCoach.Domain.Entities;

namespace TFTCoach.Application.Tests.UseCases.GetMatchHistory;

public class GetMatchHistoryUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldResolvePuuidAndReturnMatches_WhenRiotIdIsValid()
    {
        // Arrange
        var matches = new List<TftMatchResult>
        {
            new() { MatchId = "EUW_1", Placement = 1, Level = 9 },
        };
        var client = new FakeRiotMatchClient("puuid-123", matches);
        var useCase = new GetMatchHistoryUseCase(client, NullLogger<GetMatchHistoryUseCase>.Instance);

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
        var useCase = new GetMatchHistoryUseCase(client, NullLogger<GetMatchHistoryUseCase>.Instance);

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

    private sealed class FakeRiotMatchClient(string puuid, IReadOnlyList<TftMatchResult> matches) : IRiotMatchClient
    {
        public string? LastGameName { get; private set; }
        public string? LastTagLine { get; private set; }
        public int LastCount { get; private set; }

        public Task<string> ResolvePuuidAsync(string gameName, string tagLine, CancellationToken cancellationToken = default)
        {
            LastGameName = gameName;
            LastTagLine = tagLine;
            return Task.FromResult(puuid);
        }

        public Task<IReadOnlyList<TftMatchResult>> GetRecentMatchesAsync(string puuid, int count, CancellationToken cancellationToken = default)
        {
            LastCount = count;
            return Task.FromResult(matches);
        }
    }
}

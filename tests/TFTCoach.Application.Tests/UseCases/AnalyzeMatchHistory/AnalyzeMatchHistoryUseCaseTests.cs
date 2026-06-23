using Microsoft.Extensions.Logging.Abstractions;
using TFTCoach.Application.Interfaces;
using TFTCoach.Application.UseCases.AnalyzeMatchHistory;
using TFTCoach.Domain.Entities;
using TFTCoach.Domain.Interfaces;

namespace TFTCoach.Application.Tests.UseCases.AnalyzeMatchHistory;

public class AnalyzeMatchHistoryUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyState_WhenNoMatches()
    {
        // Arrange
        var inference = new FakeInferenceClient("should not be called");
        var useCase = NewUseCase(inference);

        // Act
        var response = await useCase.ExecuteAsync(new AnalyzeMatchHistoryRequest { Matches = [] });

        // Assert
        Assert.Equal(0, response.MatchesAnalyzed);
        Assert.Equal(PlacementTrend.Stable, response.Trend);
        Assert.False(inference.WasCalled);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldComputeDeterministicStats_WhenMatchesExist()
    {
        // Arrange
        var inference = new FakeInferenceClient("""{ "narrative": "ok" }""");
        var useCase = NewUseCase(inference);
        var matches = new List<TftMatchResult>
        {
            new() { MatchId = "1", Placement = 1, Level = 8, Traits = ["Sorcerer 4"] },
            new() { MatchId = "2", Placement = 3, Level = 8, Traits = ["Sorcerer 3"] },
            new() { MatchId = "3", Placement = 5, Level = 7, Traits = ["Brawler 2"] },
            new() { MatchId = "4", Placement = 7, Level = 6, Traits = ["Brawler 2"] },
        };

        // Act
        var response = await useCase.ExecuteAsync(new AnalyzeMatchHistoryRequest { Matches = matches });

        // Assert
        Assert.Equal(4, response.MatchesAnalyzed);
        Assert.Equal(4.0, response.AveragePlacement, 3);
        Assert.Equal(0.5, response.Top4Rate, 3);
        Assert.Equal(0.25, response.WinRate, 3);
        Assert.Equal(7.25, response.AverageLevel, 3);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRankTopCompsByGamesThenPlacement_WhenMatchesExist()
    {
        // Arrange
        var inference = new FakeInferenceClient("""{ "narrative": "ok" }""");
        var useCase = NewUseCase(inference);
        var matches = new List<TftMatchResult>
        {
            new() { MatchId = "1", Placement = 2, Traits = ["Sorcerer 4"] },
            new() { MatchId = "2", Placement = 4, Traits = ["Sorcerer 4"] },
            new() { MatchId = "3", Placement = 1, Traits = ["Brawler 2"] },
        };

        // Act
        var response = await useCase.ExecuteAsync(new AnalyzeMatchHistoryRequest { Matches = matches });

        // Assert
        var top = response.TopComps[0];
        Assert.Equal("Sorcerer", top.Comp);
        Assert.Equal(2, top.Games);
        Assert.Equal(3.0, top.AveragePlacement, 3);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldComputeImprovingTrend_WhenRecentPlacementsBetter()
    {
        // Arrange — older games placed worse (8/7), newer games placed better (2/1).
        var inference = new FakeInferenceClient("""{ "narrative": "ok" }""");
        var useCase = NewUseCase(inference);
        var baseTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var matches = new List<TftMatchResult>
        {
            new() { MatchId = "old1", Placement = 8, PlayedAt = baseTime },
            new() { MatchId = "old2", Placement = 7, PlayedAt = baseTime.AddHours(1) },
            new() { MatchId = "new1", Placement = 2, PlayedAt = baseTime.AddHours(2) },
            new() { MatchId = "new2", Placement = 1, PlayedAt = baseTime.AddHours(3) },
        };

        // Act
        var response = await useCase.ExecuteAsync(new AnalyzeMatchHistoryRequest { Matches = matches });

        // Assert
        Assert.Equal(PlacementTrend.Improving, response.Trend);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldParseStructuredNarrative_WhenModelReturnsJson()
    {
        // Arrange — model wraps JSON in prose + a code fence.
        const string completion = """
            Here is your review:
            ```json
            {
              "strengths": ["Strong econ management"],
              "weaknesses": ["Slow to scout"],
              "suggestions": ["Open fort more often"],
              "narrative": "You consistently top-4 with Sorcerers."
            }
            ```
            """;
        var inference = new FakeInferenceClient(completion);
        var useCase = NewUseCase(inference);
        var matches = new List<TftMatchResult>
        {
            new() { MatchId = "1", Placement = 2, Level = 8, Traits = ["Sorcerer 4"] },
        };

        // Act
        var response = await useCase.ExecuteAsync(new AnalyzeMatchHistoryRequest { Matches = matches });

        // Assert
        Assert.Equal("Strong econ management", Assert.Single(response.Strengths));
        Assert.Equal("Slow to scout", Assert.Single(response.Weaknesses));
        Assert.Equal("Open fort more often", Assert.Single(response.Suggestions));
        Assert.Equal("You consistently top-4 with Sorcerers.", response.Narrative);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFallBackToRawText_WhenModelReturnsNonJson()
    {
        // Arrange
        var inference = new FakeInferenceClient("Just some plain advice, no JSON here.");
        var useCase = NewUseCase(inference);
        var matches = new List<TftMatchResult>
        {
            new() { MatchId = "1", Placement = 4, Level = 7, Traits = ["Brawler 2"] },
        };

        // Act
        var response = await useCase.ExecuteAsync(new AnalyzeMatchHistoryRequest { Matches = matches });

        // Assert
        Assert.Equal("Just some plain advice, no JSON here.", response.Narrative);
        Assert.Empty(response.Strengths);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPersistMatches_WhenPuuidProvided()
    {
        // Arrange
        var inference = new FakeInferenceClient("""{ "narrative": "ok" }""");
        var matchRepo = new RecordingMatchHistoryRepository();
        var useCase = NewUseCase(inference, matchRepo: matchRepo);
        var matches = new List<TftMatchResult> { new() { MatchId = "1", Placement = 2 } };

        // Act
        await useCase.ExecuteAsync(new AnalyzeMatchHistoryRequest { Matches = matches, Puuid = "puuid-123" });

        // Assert
        Assert.Equal("puuid-123", matchRepo.SavedPuuid);
        Assert.Single(matchRepo.SavedMatches);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotPersist_WhenPuuidMissing()
    {
        // Arrange
        var inference = new FakeInferenceClient("""{ "narrative": "ok" }""");
        var matchRepo = new RecordingMatchHistoryRepository();
        var useCase = NewUseCase(inference, matchRepo: matchRepo);
        var matches = new List<TftMatchResult> { new() { MatchId = "1", Placement = 2 } };

        // Act
        await useCase.ExecuteAsync(new AnalyzeMatchHistoryRequest { Matches = matches });

        // Assert
        Assert.Null(matchRepo.SavedPuuid);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStillSucceed_WhenPersistenceThrows()
    {
        // Arrange — persistence is best-effort and must not fail the analysis.
        var inference = new FakeInferenceClient("""{ "narrative": "ok" }""");
        var useCase = NewUseCase(inference, matchRepo: new ThrowingMatchHistoryRepository());
        var matches = new List<TftMatchResult> { new() { MatchId = "1", Placement = 2 } };

        // Act
        var response = await useCase.ExecuteAsync(
            new AnalyzeMatchHistoryRequest { Matches = matches, Puuid = "puuid-123" });

        // Assert
        Assert.Equal(1, response.MatchesAnalyzed);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassRetrievedContextToInference_WhenVectorStoreHasMatches()
    {
        // Arrange
        var inference = new FakeInferenceClient("""{ "narrative": "ok" }""");
        var vectorRepo = new StubVectorRepository(["Sorcerers were buffed this patch."]);
        var useCase = NewUseCase(inference, vectorRepo: vectorRepo);
        var matches = new List<TftMatchResult>
        {
            new() { MatchId = "1", Placement = 2, Traits = ["Sorcerer 4"] },
        };

        // Act
        await useCase.ExecuteAsync(new AnalyzeMatchHistoryRequest { Matches = matches });

        // Assert
        Assert.Contains("Sorcerers were buffed this patch.", inference.LastUserMessage);
    }

    private static AnalyzeMatchHistoryUseCase NewUseCase(
        ILMStudioClient inference,
        IVectorRepository? vectorRepo = null,
        IMatchHistoryRepository? matchRepo = null)
        => new(
            inference,
            vectorRepo ?? new StubVectorRepository([]),
            matchRepo ?? new RecordingMatchHistoryRepository(),
            NullLogger<AnalyzeMatchHistoryUseCase>.Instance);

    private sealed class FakeInferenceClient(string completion) : ILMStudioClient
    {
        public bool WasCalled { get; private set; }
        public string LastUserMessage { get; private set; } = string.Empty;

        public Task<IReadOnlyList<float>> EmbedAsync(string text, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<float>>([0.1f, 0.2f, 0.3f]);

        public Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastUserMessage = userMessage;
            return Task.FromResult(completion);
        }
    }

    private sealed class StubVectorRepository(IReadOnlyList<string> results) : IVectorRepository
    {
        public Task<int> UpsertAsync(IReadOnlyList<PatchDocument> documents, CancellationToken cancellationToken = default)
            => Task.FromResult(documents.Count);

        public Task<IReadOnlyList<string>> SearchAsync(IReadOnlyList<float> embedding, int topK, CancellationToken cancellationToken = default)
            => Task.FromResult(results);
    }

    private sealed class RecordingMatchHistoryRepository : IMatchHistoryRepository
    {
        public string? SavedPuuid { get; private set; }
        public IReadOnlyList<TftMatchResult> SavedMatches { get; private set; } = [];

        public Task SaveAsync(string puuid, IReadOnlyList<TftMatchResult> matches, CancellationToken cancellationToken = default)
        {
            SavedPuuid = puuid;
            SavedMatches = matches;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TftMatchResult>> GetByPuuidAsync(string puuid, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TftMatchResult>>([]);
    }

    private sealed class ThrowingMatchHistoryRepository : IMatchHistoryRepository
    {
        public Task SaveAsync(string puuid, IReadOnlyList<TftMatchResult> matches, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("db down");

        public Task<IReadOnlyList<TftMatchResult>> GetByPuuidAsync(string puuid, int limit, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TftMatchResult>>([]);
    }
}

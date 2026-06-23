using Microsoft.Extensions.Logging.Abstractions;
using TFTCoach.Application.Interfaces;
using TFTCoach.Application.UseCases.AnalyzeMatchHistory;
using TFTCoach.Domain.Entities;

namespace TFTCoach.Application.Tests.UseCases.AnalyzeMatchHistory;

public class AnalyzeMatchHistoryUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnEmptyState_WhenNoMatches()
    {
        // Arrange
        var inference = new FakeInferenceClient("should not be called");
        var useCase = new AnalyzeMatchHistoryUseCase(inference, NullLogger<AnalyzeMatchHistoryUseCase>.Instance);

        // Act
        var response = await useCase.ExecuteAsync(new AnalyzeMatchHistoryRequest { Matches = [] });

        // Assert
        Assert.Equal(0, response.MatchesAnalyzed);
        Assert.False(inference.WasCalled);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSummarizeMatchesAndCallInference_WhenMatchesExist()
    {
        // Arrange
        var inference = new FakeInferenceClient("- You play fast 8.");
        var useCase = new AnalyzeMatchHistoryUseCase(inference, NullLogger<AnalyzeMatchHistoryUseCase>.Instance);
        var matches = new List<TftMatchResult>
        {
            new() { MatchId = "1", Placement = 2, Level = 8, Traits = ["Sorcerer 4"], Units = ["Ahri"] },
        };

        // Act
        var response = await useCase.ExecuteAsync(new AnalyzeMatchHistoryRequest { Matches = matches });

        // Assert
        Assert.Equal(1, response.MatchesAnalyzed);
        Assert.Equal("- You play fast 8.", response.Insights);
        Assert.True(inference.WasCalled);
        Assert.Contains("Placement 2", inference.LastUserMessage);
        Assert.Contains("Sorcerer 4", inference.LastUserMessage);
    }

    private sealed class FakeInferenceClient(string completion) : ILMStudioClient
    {
        public bool WasCalled { get; private set; }
        public string LastUserMessage { get; private set; } = string.Empty;

        public Task<IReadOnlyList<float>> EmbedAsync(string text, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<float>>([]);

        public Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            LastUserMessage = userMessage;
            return Task.FromResult(completion);
        }
    }
}

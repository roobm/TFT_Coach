using Microsoft.Extensions.Logging.Abstractions;
using TFTCoach.Application.Interfaces;
using TFTCoach.Application.UseCases.GetSuggestion;
using TFTCoach.Domain.Entities;

namespace TFTCoach.Application.Tests.UseCases.GetSuggestion;

public class GetSuggestionUseCaseTests
{
    [Theory]
    [InlineData("[ECONOMY] Hold at 50 gold and level next round.", SuggestionCategory.Economy)]
    [InlineData("[BOARD] Move your carry to the backline.", SuggestionCategory.Board)]
    [InlineData("[ITEMS] Build Guinsoo on Aphelios now.", SuggestionCategory.Items)]
    [InlineData("[COMP] Pivot into Sorcerers off your augments.", SuggestionCategory.Comp)]
    [InlineData("[SCOUT] Scout the lobby before you roll.", SuggestionCategory.Scout)]
    public async Task ExecuteAsync_ShouldParseCategory_WhenModelLeadsWithTag(
        string completion,
        SuggestionCategory expected)
    {
        // Arrange
        var inference = new FakeInferenceClient(completion);
        var useCase = NewUseCase(inference);

        // Act
        var response = await useCase.ExecuteAsync(NewRequest());

        // Assert
        Assert.Equal(expected, response.Category);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStripTagFromText_WhenModelLeadsWithTag()
    {
        // Arrange
        var inference = new FakeInferenceClient("[BOARD] Move your carry to the backline.");
        var useCase = NewUseCase(inference);

        // Act
        var response = await useCase.ExecuteAsync(NewRequest());

        // Assert
        Assert.Equal("Move your carry to the backline.", response.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDefaultToBoard_WhenTagMissing()
    {
        // Arrange — a malformed completion with no leading tag still yields advice.
        var inference = new FakeInferenceClient("Roll down to stabilise your board.");
        var useCase = NewUseCase(inference);

        // Act
        var response = await useCase.ExecuteAsync(NewRequest());

        // Assert
        Assert.Equal(SuggestionCategory.Board, response.Category);
        Assert.Equal("Roll down to stabilise your board.", response.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDefaultToBoard_WhenTagUnrecognised()
    {
        // Arrange
        var inference = new FakeInferenceClient("[POSITION] Move your carry away from the assassin.");
        var useCase = NewUseCase(inference);

        // Act
        var response = await useCase.ExecuteAsync(NewRequest());

        // Assert
        Assert.Equal(SuggestionCategory.Board, response.Category);
        Assert.Equal("[POSITION] Move your carry away from the assassin.", response.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIncludeBoardStateInPrompt_WhenStateProvided()
    {
        // Arrange
        var inference = new FakeInferenceClient("[ECONOMY] Hold for interest.");
        var useCase = NewUseCase(inference);
        var request = new GetSuggestionRequest
        {
            State = new GameState
            {
                Stage = "3-2",
                Gold = 52,
                Level = 7,
                Health = 60,
                Streak = 3,
                Units = ["Aphelios", "Jinx"],
                Items = ["Guinsoo", "Tear"],
                Notes = "Contested by the player to my left.",
            },
        };

        // Act
        await useCase.ExecuteAsync(request);

        // Assert
        Assert.Contains("3-2", inference.LastUserMessage);
        Assert.Contains("52", inference.LastUserMessage);
        Assert.Contains("Aphelios", inference.LastUserMessage);
        Assert.Contains("Guinsoo", inference.LastUserMessage);
        Assert.Contains("win streak", inference.LastUserMessage);
        Assert.Contains("Contested by the player to my left.", inference.LastUserMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGroundPromptInMeta_WhenMetaAvailable()
    {
        // Arrange
        var inference = new FakeInferenceClient("[COMP] Commit to Cypher Vanguard.");
        var meta = new MetaSnapshot
        {
            Set = "Set 14: Cyber City",
            Patch = "14.6",
            Comps =
            [
                new MetaComp
                {
                    Name = "Cypher Vanguard",
                    Tier = "S",
                    Carries = ["Aphelios"],
                    KeyItems = ["Guinsoo's Rageblade"],
                },
            ],
        };
        var useCase = NewUseCase(inference, new FakeMetaProvider(meta));

        // Act
        await useCase.ExecuteAsync(NewRequest());

        // Assert
        Assert.Contains("14.6", inference.LastUserMessage);
        Assert.Contains("Cypher Vanguard", inference.LastUserMessage);
        Assert.Contains("Guinsoo's Rageblade", inference.LastUserMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldOmitMetaSection_WhenNoMetaAvailable()
    {
        // Arrange
        var inference = new FakeInferenceClient("[ECONOMY] Hold for interest.");
        var useCase = NewUseCase(inference, new FakeMetaProvider(null));

        // Act
        await useCase.ExecuteAsync(NewRequest());

        // Assert
        Assert.DoesNotContain("Current meta", inference.LastUserMessage);
    }

    private static GetSuggestionRequest NewRequest() => new()
    {
        State = new GameState { Stage = "3-2", Gold = 30, Level = 7, Health = 50 },
    };

    private static GetSuggestionUseCase NewUseCase(
        ILMStudioClient inference,
        IMetaProvider? metaProvider = null)
        => new(inference, metaProvider ?? new FakeMetaProvider(null), NullLogger<GetSuggestionUseCase>.Instance);

    private sealed class FakeInferenceClient(string completion) : ILMStudioClient
    {
        public string LastUserMessage { get; private set; } = string.Empty;

        public Task<IReadOnlyList<float>> EmbedAsync(string text, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<float>>([0.1f, 0.2f, 0.3f]);

        public Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
        {
            LastUserMessage = userMessage;
            return Task.FromResult(completion);
        }
    }

    private sealed class FakeMetaProvider(MetaSnapshot? meta) : IMetaProvider
    {
        public Task<MetaSnapshot?> GetCurrentMetaAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(meta);
    }
}

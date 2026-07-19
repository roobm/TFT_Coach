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

    [Fact]
    public async Task ExecuteAsync_ShouldFormatBulletList_WhenModelEmitsMultipleLines()
    {
        // Arrange — a multi-line bullet answer is kept as separate, marker-free lines.
        var inference = new FakeInferenceClient(
            "[BOARD]\n- Move your carry to the backline.\n- Keep your tank up front.");
        var useCase = NewUseCase(inference);

        // Act
        var response = await useCase.ExecuteAsync(NewRequest());

        // Assert
        Assert.Equal("Move your carry to the backline.\nKeep your tank up front.", response.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCapBullets_WhenModelOverruns()
    {
        // Arrange — more than the cap of bullets is trimmed to the first five.
        var inference = new FakeInferenceClient(
            "[ECONOMY]\n- One.\n- Two.\n- Three.\n- Four.\n- Five.\n- Six.");
        var useCase = NewUseCase(inference);

        // Act
        var response = await useCase.ExecuteAsync(NewRequest());

        // Assert
        Assert.Equal(5, response.Text.Split('\n').Length);
        Assert.DoesNotContain("Six", response.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldKeepFirstBlock_WhenModelRestartsWithSecondTag()
    {
        // Arrange — a reasoning model sometimes rambles into a second answer block;
        // only the first should survive.
        var inference = new FakeInferenceClient(
            "[ECONOMY]\n- Hold at 50 gold and level to 7.\n- Greed for interest.\n\n[HOLD]\n- Hold at 50 gold and level to 7.\n- Maximise your gold first.");
        var useCase = NewUseCase(inference);

        // Act
        var response = await useCase.ExecuteAsync(NewRequest());

        // Assert
        Assert.Equal(SuggestionCategory.Economy, response.Category);
        Assert.Equal("Hold at 50 gold and level to 7.\nGreed for interest.", response.Text);
        Assert.DoesNotContain("HOLD", response.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStripThinkBlock_WhenModelReasons()
    {
        // Arrange — Qwen3's private reasoning must not reach the player.
        var inference = new FakeInferenceClient(
            "<think>\nThe board already fits Cypher Vanguard, so commit.\n</think>\n[COMP]\n- Commit to Cypher Vanguard now.");
        var useCase = NewUseCase(inference);

        // Act
        var response = await useCase.ExecuteAsync(NewRequest());

        // Assert
        Assert.Equal(SuggestionCategory.Comp, response.Category);
        Assert.Equal("Commit to Cypher Vanguard now.", response.Text);
        Assert.DoesNotContain("<think>", response.Text);
        Assert.DoesNotContain("so commit", response.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPreserveActionVerbFirst_WhenNormalising()
    {
        // Arrange — the leading action-verb bullet must stay first after normalising.
        var inference = new FakeInferenceClient(
            "[ITEMS]\n- Build Guinsoo on Aphelios now.\n- Slam the components.\n- Do not hold.");
        var useCase = NewUseCase(inference);

        // Act
        var response = await useCase.ExecuteAsync(NewRequest());

        // Assert
        Assert.StartsWith("Build ", response.Text);
        Assert.Equal("Build Guinsoo on Aphelios now.\nSlam the components.\nDo not hold.", response.Text);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSurfaceBoardFit_WhenUnitsOverlapMetaComp()
    {
        // Arrange — current units overlap a listed comp's carries (incl. a star-decorated label).
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
                    Carries = ["Aphelios", "Jinx"],
                    KeyItems = ["Guinsoo's Rageblade"],
                },
            ],
        };
        var request = new GetSuggestionRequest
        {
            State = new GameState
            {
                Stage = "4-1",
                Gold = 50,
                Level = 7,
                Health = 60,
                Units = ["Aphelios 2\u2605", "Jinx"],
            },
        };
        var useCase = NewUseCase(inference, new FakeMetaProvider(meta));

        // Act
        await useCase.ExecuteAsync(request);

        // Assert
        Assert.Contains("Board fit:", inference.LastUserMessage);
        Assert.Contains("Cypher Vanguard (2 matching carries)", inference.LastUserMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFlagFirstPrinciples_WhenUnitsMatchNoMetaComp()
    {
        // Arrange — units share nothing with the listed comp's carries.
        var inference = new FakeInferenceClient("[COMP] Pivot toward the strongest line.");
        var meta = new MetaSnapshot
        {
            Set = "Set 14: Cyber City",
            Patch = "14.6",
            Comps =
            [
                new MetaComp { Name = "Cypher Vanguard", Tier = "S", Carries = ["Aphelios"] },
            ],
        };
        var request = new GetSuggestionRequest
        {
            State = new GameState { Stage = "3-2", Gold = 30, Level = 6, Health = 70, Units = ["Garen", "Ahri"] },
        };
        var useCase = NewUseCase(inference, new FakeMetaProvider(meta));

        // Act
        await useCase.ExecuteAsync(request);

        // Assert
        Assert.Contains("Board fit: your current units do not match any listed comp", inference.LastUserMessage);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGroundSystemPromptInTimingHeuristics_WhenInvoked()
    {
        // Arrange
        var inference = new FakeInferenceClient("[ECONOMY] Hold for interest.");
        var useCase = NewUseCase(inference);

        // Act
        await useCase.ExecuteAsync(NewRequest());

        // Assert — heuristics, board-judgement guidance and a per-tag few-shot are present.
        Assert.Contains("TFT TIMING HEURISTICS", inference.LastSystemPrompt);
        Assert.Contains("Interest pays 1 gold per 10 banked", inference.LastSystemPrompt);
        Assert.Contains("JUDGEMENT", inference.LastSystemPrompt);
        Assert.Contains("DO NOT advise pivoting off it", inference.LastSystemPrompt);
        Assert.Contains("Commit to the listed comp your core units already fit.", inference.LastSystemPrompt);
        Assert.Contains("Hold at 50 gold and level to 7 on 4-1.", inference.LastSystemPrompt);
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

        public string LastSystemPrompt { get; private set; } = string.Empty;

        public Task<IReadOnlyList<float>> EmbedAsync(string text, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<float>>([0.1f, 0.2f, 0.3f]);

        public Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
        {
            LastSystemPrompt = systemPrompt;
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

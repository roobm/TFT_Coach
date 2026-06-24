using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TFTCoach.Application.Interfaces;
using TFTCoach.Domain.Entities;

namespace TFTCoach.Application.UseCases.GetSuggestion;

/// <summary>
/// Produces a single, real-time coaching suggestion from one snapshot of the
/// player's board. The board state is rendered into a compact prompt and the
/// local LM Studio model returns one tagged line; the tag is parsed into a
/// <see cref="SuggestionCategory"/> and stripped from the advice text.
/// Stateless — no conversational history is kept between calls.
/// </summary>
public sealed partial class GetSuggestionUseCase(
    ILMStudioClient inference,
    ILogger<GetSuggestionUseCase> logger)
{
    private const string SystemPrompt =
        "You are a Challenger-level Teamfight Tactics coach giving ONE live, in-game suggestion. " +
        "You are given a single snapshot of the player's current board: stage, gold, level, health, " +
        "win/loss streak, the units on their board, and the items/components they hold. " +
        "Decide the single highest-impact action for THIS moment.\n\n" +
        "RULES (non-negotiable):\n" +
        "- Reply with EXACTLY ONE line and nothing else.\n" +
        "- Begin with exactly one tag in square brackets: [ECONOMY], [BOARD], [ITEMS], [COMP], or [SCOUT].\n" +
        "- After the tag, write at most TWO sentences.\n" +
        "- The first sentence MUST begin with an action verb: Sell, Buy, Roll, Hold, Move, Build, Pivot, or Scout.\n" +
        "- Be concrete and tie the advice to the numbers and units shown. Never invent units, items, or numbers.\n" +
        "- Pick the tag that matches the advice: [ECONOMY] gold/interest/leveling, [BOARD] units & positioning, " +
        "[ITEMS] item building/holding, [COMP] which composition to commit or pivot to, [SCOUT] checking opponents.\n\n" +
        "Example: [ECONOMY] Hold at 50 gold and level to 7 next round, then roll. You are healthy enough to greed for interest.";

    public async Task<GetSuggestionResponse> ExecuteAsync(
        GetSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userMessage = BuildPrompt(request.State);
        // Qwen3 hybrid-reasoning soft switch: /no_think must be the last token of the
        // user turn so the model answers directly instead of emitting chain-of-thought.
        var raw = await inference.CompleteAsync(SystemPrompt, userMessage + "\n\n/no_think", cancellationToken);
        var suggestion = ParseSuggestion(raw);

        logger.LogInformation(
            "Generated {Category} suggestion for stage {Stage} (level {Level}, {Gold} gold).",
            suggestion.Category, request.State.Stage, request.State.Level, request.State.Gold);

        return new GetSuggestionResponse
        {
            Category = suggestion.Category,
            Text = suggestion.Text,
        };
    }

    private static string BuildPrompt(GameState state)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Current board snapshot:");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Stage: {Display(state.Stage)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Gold: {state.Gold}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Level: {state.Level}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Health: {state.Health}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Streak: {DescribeStreak(state.Streak)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Board units: {DisplayList(state.Units)}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"- Items / components: {DisplayList(state.Items)}");

        if (!string.IsNullOrWhiteSpace(state.Notes))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"- Player notes: {state.Notes.Trim()}");
        }

        sb.Append("Give the single highest-impact suggestion for this moment.");
        return sb.ToString();
    }

    private static string Display(string value) =>
        string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();

    private static string DisplayList(IReadOnlyList<string> values)
    {
        var cleaned = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim());
        var joined = string.Join(", ", cleaned);
        return joined.Length == 0 ? "none" : joined;
    }

    private static string DescribeStreak(int streak) => streak switch
    {
        > 0 => $"{streak}-game win streak",
        < 0 => $"{-streak}-game loss streak",
        _ => "no streak",
    };

    // The model is told to lead with a [TAG]; parse it into a category and strip it
    // from the advice. If the tag is missing or unrecognised, default to Board so a
    // malformed completion still yields a usable suggestion.
    private static CoachingSuggestion ParseSuggestion(string raw)
    {
        var text = raw.Trim();
        var match = TagPattern().Match(text);
        var category = SuggestionCategory.Board;

        if (match.Success)
        {
            category = match.Groups[1].Value.ToUpperInvariant() switch
            {
                "ECONOMY" => SuggestionCategory.Economy,
                "BOARD" => SuggestionCategory.Board,
                "ITEMS" => SuggestionCategory.Items,
                "COMP" => SuggestionCategory.Comp,
                "SCOUT" => SuggestionCategory.Scout,
                _ => SuggestionCategory.Board,
            };
            text = text[match.Length..].Trim();
        }

        return new CoachingSuggestion { Category = category, Text = text };
    }

    [GeneratedRegex(@"^\[\s*(ECONOMY|BOARD|ITEMS|COMP|SCOUT)\s*\]\s*", RegexOptions.IgnoreCase)]
    private static partial Regex TagPattern();
}

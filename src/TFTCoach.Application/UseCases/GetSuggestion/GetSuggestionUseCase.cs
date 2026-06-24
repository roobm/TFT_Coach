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
    IMetaProvider metaProvider,
    ILogger<GetSuggestionUseCase> logger)
{
    // Cap how many comps are rendered into the prompt so the meta section stays
    // compact and the model focuses on the strongest options.
    private const int MaxMetaComps = 6;

    // Cap how many bullets we keep from the model's answer so the card stays scannable.
    private const int MaxBullets = 5;

    private const string SystemPrompt =
        "You are a Challenger-level Teamfight Tactics coach giving ONE live, in-game recommendation. " +
        "You receive a single snapshot of the player's board: stage, gold, level, health, win/loss streak, " +
        "the units they field, the items/components they hold, the current meta comps, and a board-fit read.\n\n" +
        "Reason privately first: judge how strong the current board is, which meta comp it already fits, the " +
        "economy/health/level timing, and the single highest-impact line. Then output only your final answer.\n\n" +
        "OUTPUT (non-negotiable):\n" +
        "- First line: EXACTLY ONE tag in square brackets and nothing else on that line: [ECONOMY], [BOARD], [ITEMS], [COMP], or [SCOUT].\n" +
        "- Then 2 to 4 bullet points, each on its own line starting with \"- \".\n" +
        "- The FIRST bullet is the single highest-impact action and must begin with an action verb: Sell, Buy, Roll, Hold, Move, Build, Slam, Level, Commit, Pivot, Reposition, or Scout.\n" +
        "- The remaining bullets justify it with concrete reasoning: economy math, timing, positioning, or what to watch.\n" +
        "- Keep each bullet to one short sentence. Use ONLY the numbers, units, items, and meta comps provided \u2014 never invent any.\n\n" +
        "JUDGEMENT (read the board before you speak):\n" +
        "- If the board already fits a strong listed comp, DO NOT advise pivoting off it \u2014 reinforce it by levelling for traits, rolling for star-ups, itemising the carry, or fixing positioning.\n" +
        "- Only recommend a pivot when the current units clearly fit no strong line and a better one is reachable.\n" +
        "- Pick the tag that matches the action you are recommending.\n\n" +
        "TAG SELECTION:\n" +
        "- [ECONOMY]: gold, interest breakpoints, and when to level vs roll.\n" +
        "- [BOARD]: which units to field, star-ups, and frontline/backline positioning.\n" +
        "- [ITEMS]: which items/components to build or hold, and on whom.\n" +
        "- [COMP]: which composition to commit to or pivot toward.\n" +
        "- [SCOUT]: checking opponents' boards, items, or threats.\n\n" +
        "TFT TIMING HEURISTICS (apply to the snapshot):\n" +
        "- Interest pays 1 gold per 10 banked, capped at 5 at 50 gold; protect the 10/20/30/40/50 breakpoints unless health forces a roll.\n" +
        "- Greed for economy and push levels when health is high (>50); stabilise and roll when health is low (<35) even at an economy cost.\n" +
        "- Standard tempo: reach level 6 by 3-2, level 7 around 4-1, level 8 around 4-5/5-1. Roll on a level to find your carries, then stop.\n" +
        "- Slow-roll at 50 gold (level 6 for 2-cost or level 7 for 3-cost carries) when chasing 3-stars; otherwise roll to stabilise or push levels for tempo.\n" +
        "- A win streak or loss streak is gold, so ride it; break a streak only to save health or hit a key power spike.\n\n" +
        "EXAMPLES (format only \u2014 match this shape, not these words):\n" +
        "[COMP]\n" +
        "- Commit to the listed comp your core units already fit.\n" +
        "- Level to 8 on 4-5 to unlock your last trait breakpoint.\n" +
        "- Roll only down to about 30 gold to find your carry upgrades.\n\n" +
        "[ECONOMY]\n" +
        "- Hold at 50 gold and level to 7 on 4-1.\n" +
        "- Your health is high enough to greed for interest.";

    public async Task<GetSuggestionResponse> ExecuteAsync(
        GetSuggestionRequest request,
        CancellationToken cancellationToken = default)
    {
        var meta = await metaProvider.GetCurrentMetaAsync(cancellationToken);
        var userMessage = BuildPrompt(request.State, meta);
        // Let Qwen3 reason: we deliberately do NOT append /no_think so the model thinks
        // through board strength, comp fit and timing before answering. Its private
        // <think> block is stripped from the completion in ParseSuggestion.
        var raw = await inference.CompleteAsync(SystemPrompt, userMessage, cancellationToken);
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

    private static string BuildPrompt(GameState state, MetaSnapshot? meta)
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

        AppendMetaSection(sb, meta);
        AppendBoardFitSection(sb, state, meta);

        sb.Append("Give the single highest-impact suggestion for this moment.");
        return sb.ToString();
    }

    // Renders the current meta as a compact, patch-tagged list the model can
    // steer toward. Skipped entirely when no meta is available.
    private static void AppendMetaSection(StringBuilder sb, MetaSnapshot? meta)
    {
        if (meta is null || meta.Comps.Count == 0)
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Current meta ({meta.Set}, patch {meta.Patch}) \u2014 prefer steering toward one of these when it fits:");
        foreach (var comp in meta.Comps.Take(MaxMetaComps))
        {
            var tier = string.IsNullOrWhiteSpace(comp.Tier) ? "" : $" [{comp.Tier.Trim()}]";
            sb.Append(CultureInfo.InvariantCulture, $"- {comp.Name.Trim()}{tier}:");
            if (comp.Carries.Count > 0)
            {
                sb.Append(CultureInfo.InvariantCulture, $" carries {DisplayList(comp.Carries)};");
            }
            if (comp.KeyItems.Count > 0)
            {
                sb.Append(CultureInfo.InvariantCulture, $" items {DisplayList(comp.KeyItems)};");
            }
            if (!string.IsNullOrWhiteSpace(comp.Playstyle))
            {
                sb.Append(CultureInfo.InvariantCulture, $" {comp.Playstyle.Trim()}.");
            }
            if (!string.IsNullOrWhiteSpace(comp.Notes))
            {
                sb.Append(CultureInfo.InvariantCulture, $" {comp.Notes.Trim()}");
            }
            sb.AppendLine();
        }
    }

    // Grounds the model in the board->meta mapping: counts how many of each comp's
    // carries already appear among the player's (possibly star/item-decorated) units
    // and surfaces the best-fitting comps. When nothing overlaps, it explicitly tells
    // the model to advise from first principles so it does not force an off-board comp.
    private static void AppendBoardFitSection(StringBuilder sb, GameState state, MetaSnapshot? meta)
    {
        if (meta is null || meta.Comps.Count == 0 || state.Units.Count == 0)
        {
            return;
        }

        var fits = meta.Comps
            .Select(comp => (comp.Name, Overlap: CountCarryOverlap(state.Units, comp)))
            .Where(x => x.Overlap > 0)
            .OrderByDescending(x => x.Overlap)
            .Take(3)
            .ToList();

        sb.AppendLine();
        if (fits.Count == 0)
        {
            sb.AppendLine(
                "Board fit: your current units do not match any listed comp \u2014 advise from first principles toward the strongest reachable line.");
            return;
        }

        var parts = fits.Select(f =>
            $"{f.Name.Trim()} ({f.Overlap} matching carr{(f.Overlap == 1 ? "y" : "ies")})");
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"Board fit: your units overlap {string.Join("; ", parts)} \u2014 steer toward the best fit.");
    }

    // Case-insensitive substring match so a decoded label like "Aphelios 2\u2605"
    // still counts toward a meta carry named "Aphelios".
    private static int CountCarryOverlap(IReadOnlyList<string> units, MetaComp comp)
    {
        var unitText = string.Join(
            " | ",
            units.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.ToLowerInvariant()));
        if (unitText.Length == 0)
        {
            return 0;
        }

        return comp.Carries.Count(c =>
            !string.IsNullOrWhiteSpace(c) &&
            unitText.Contains(c.Trim().ToLowerInvariant(), StringComparison.Ordinal));
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
    // malformed completion still yields a usable suggestion. The model's private
    // <think> reasoning is removed first, then the answer is tidied into a clean
    // bullet list. Neither step fabricates content — they only strip and normalise
    // what the model returned.
    private static CoachingSuggestion ParseSuggestion(string raw)
    {
        var text = StripThink(raw).Trim();
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
            text = text[match.Length..];
        }

        text = NormalizeBullets(text);

        return new CoachingSuggestion { Category = category, Text = text };
    }

    // Qwen3 emits its reasoning inside a <think>...</think> block before the answer.
    // Strip it (including an unclosed block from a truncated completion) so only the
    // final recommendation reaches the player.
    private static string StripThink(string text)
    {
        var stripped = ThinkPattern().Replace(text, string.Empty);
        var open = stripped.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
        return open >= 0 ? stripped[..open] : stripped;
    }

    // Normalises the model's answer into a clean newline-separated bullet list.
    // Leading list markers (-, *, •, 1.) are stripped so the frontend renders its
    // own bullets, blank lines are dropped, and the list is capped. A reasoning model
    // sometimes restarts with a second tag block (e.g. "[HOLD]"); we stop at the first
    // bracketed-tag line so only the first answer survives. This only tidies what the
    // model returned \u2014 it never fabricates content.
    private static string NormalizeBullets(string text)
    {
        var bullets = new List<string>();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = BulletPattern().Replace(rawLine.Trim(), string.Empty).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            // A line that restarts with a bracketed tag means the model began a second
            // answer block; keep only the first one.
            if (line.StartsWith('['))
            {
                break;
            }

            bullets.Add(line);
            if (bullets.Count == MaxBullets)
            {
                break;
            }
        }

        return bullets.Count == 0 ? text.Trim() : string.Join('\n', bullets);
    }

    [GeneratedRegex(@"^\[\s*(ECONOMY|BOARD|ITEMS|COMP|SCOUT)\s*\]\s*", RegexOptions.IgnoreCase)]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"<think>.*?</think>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ThinkPattern();

    [GeneratedRegex(@"^\s*(?:[-*\u2022\u2013]|\d+[.)])\s*")]
    private static partial Regex BulletPattern();
}

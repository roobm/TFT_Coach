using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TFTCoach.Application.Interfaces;
using TFTCoach.Domain.Entities;
using TFTCoach.Domain.Interfaces;

namespace TFTCoach.Application.UseCases.AnalyzeMatchHistory;

/// <summary>
/// Produces an in-depth, structured analysis of a player's recent matches.
/// Numeric stats and trends are computed deterministically in code; relevant patch
/// context is retrieved from the vector store (RAG); local LM Studio writes the
/// narrative, strengths, weaknesses, and suggestions grounded in that data.
/// </summary>
public sealed class AnalyzeMatchHistoryUseCase(
    ILMStudioClient inference,
    IVectorRepository vectorRepository,
    IMatchHistoryRepository matchHistoryRepository,
    IMetaProvider metaProvider,
    ILogger<AnalyzeMatchHistoryUseCase> logger)
{
    private const int TopCompsCount = 3;
    private const int PatchContextCount = 5;

    /// <summary>Cap how many meta comps are rendered so the meta section stays compact.</summary>
    private const int MaxMetaComps = 6;

    /// <summary>Minimum placement delta between halves before a trend is non-stable.</summary>
    private const double TrendThreshold = 0.5;

    private const string SystemPrompt =
        "You are a Challenger-level Teamfight Tactics coach writing an in-depth post-game review for a " +
        "student who wants to climb. You are given a fixed block of data: matches analyzed, average " +
        "placement, top-4 rate, win rate (1st), average level, a placement trend (Improving / Declining / " +
        "Stable), the player's most-played comps (comp | games | avg placement), per-match detail " +
        "placement, level, traits, augments, units), the current meta comps, a meta-fit read of how the " +
        "player's comps map to that meta, and optional patch/meta context.\n\n" +
        "GROUNDING (non-negotiable):\n" +
        "- Every number, rate, trend, and comp is a pre-computed FACT. Treat them as authoritative. Never " +
        "recompute, re-average, contradict, or invent numbers, champions, traits, augments, or items.\n" +
        "- Base every claim on the data shown. Cite the specific evidence behind it — an exact stat (e.g. " +
        "avg placement, top-4 rate, average level), the trend, or a recurring comp/trait/augment/unit you " +
        "can see across matches.\n" +
        "- Only use patch/meta context when it is provided, and only to interpret what the player did. Do " +
        "not assess positioning, scouting, mana, or item components — that data is NOT provided, so do not " +
        "speculate about it.\n" +
        "- Use the current meta comps and the meta-fit read ONLY when provided: lean on them to judge whether " +
        "the player is on- or off-meta and to name their closest meta comp. Never invent comps or champions " +
        "not shown; if the fit read says they are off-meta, advise from first principles instead.\n" +
        "- If the sample is small or signals conflict, say so plainly rather than overstating a pattern.\n\n" +
        "COACHING LENS — judge the player on these and name the dimension you are invoking:\n" +
        "- Economy: average level vs. placement as a proxy for gold/interest management and greed vs. spend.\n" +
        "- Tempo & leveling: whether average level and outcomes suggest pushing levels vs. holding, fast 8 " +
        "vs. slow roll, HP spent well or hemorrhaged.\n" +
        "- Rolldown: do strong boards (low placements) line up with the levels reached, or are they rolling " +
        "off-stage / over-rolling?\n" +
        "- Board strength: trait breakpoints and unit choices in the per-match detail; capped vs. uncapped " +
        "boards.\n" +
        "- Itemization: which augments/units recur on good vs. bad finishes (item components are not given).\n" +
        "- Comp flexibility: are they forcing one line every game, or do the top comps and per-match traits " +
        "show pivoting to the board/augments they're given?\n" +
        "- Consistency: does the spread of placements show fishing for 1sts and bleeding out, or stable " +
        "top-4? Reconcile this with win rate, top-4 rate, and the trend.\n\n" +
        "OUTPUT — respond with a SINGLE JSON object and nothing else (no prose, no code fences), using " +
        "EXACTLY these keys:\n" +
        "- \"strengths\": array of strings. Each is one specific, evidence-backed habit that is working, " +
        "citing the stat/comp/trait it rests on and the lens it belongs to.\n" +
        "- \"weaknesses\": array of strings. Same rule — concrete, evidenced, no generic filler.\n" +
        "- \"suggestions\": array of strings, ordered MOST-IMPACTFUL FIRST. Each leads with an action verb " +
        "(Hold, Roll, Pivot, Slam, Level, Push, Greed, Stabilise) and states the change and the expected " +
        "payoff in LP/placement, grounded in this player's own data.\n" +
        "- \"narrative\": a string of 3-5 sentences capturing the player's play style and naming the SINGLE " +
        "biggest lever that will gain them the most LP, tied directly to their stats and comps.\n" +
        "This is long-form analysis: be substantive, specific, and prioritised — never a one-line tip and " +
        "never generic platitudes.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AnalyzeMatchHistoryResponse> ExecuteAsync(
        AnalyzeMatchHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var matches = request.Matches;
        if (matches.Count == 0)
        {
            return EmptyResponse();
        }

        var stats = ComputeStats(matches);
        var topComps = ComputeTopComps(matches);
        var trend = ComputeTrend(matches);

        await PersistAsync(request.Puuid, matches, cancellationToken);

        var patchContext = await RetrievePatchContextAsync(topComps, matches, cancellationToken);
        var meta = await RetrieveMetaAsync(cancellationToken);

        var userMessage = BuildPrompt(stats, topComps, trend, matches, patchContext, meta);
        // Qwen3 hybrid-reasoning soft switch: /no_think must be the last token of the
        // user turn to suppress the model's chain-of-thought and answer directly.
        var raw = await inference.CompleteAsync(SystemPrompt, userMessage + "\n\n/no_think", cancellationToken);
        var narrative = ParseNarrative(raw);

        logger.LogInformation("Generated structured analysis from {Count} matches", matches.Count);

        return new AnalyzeMatchHistoryResponse
        {
            MatchesAnalyzed = matches.Count,
            AveragePlacement = stats.AveragePlacement,
            Top4Rate = stats.Top4Rate,
            WinRate = stats.WinRate,
            AverageLevel = stats.AverageLevel,
            Trend = trend,
            TopComps = topComps,
            Strengths = narrative.Strengths,
            Weaknesses = narrative.Weaknesses,
            Suggestions = narrative.Suggestions,
            Narrative = narrative.Narrative,
        };
    }

    private static AnalyzeMatchHistoryResponse EmptyResponse() => new()
    {
        MatchesAnalyzed = 0,
        AveragePlacement = 0,
        Top4Rate = 0,
        WinRate = 0,
        AverageLevel = 0,
        Trend = PlacementTrend.Stable,
        TopComps = [],
        Strengths = [],
        Weaknesses = [],
        Suggestions = [],
        Narrative = "No matches available to analyze.",
    };

    private static Stats ComputeStats(IReadOnlyList<TftMatchResult> matches)
    {
        var count = matches.Count;
        return new Stats(
            AveragePlacement: matches.Average(m => m.Placement),
            Top4Rate: matches.Count(m => m.Placement <= 4) / (double)count,
            WinRate: matches.Count(m => m.Placement == 1) / (double)count,
            AverageLevel: matches.Average(m => m.Level));
    }

    private static IReadOnlyList<CompPerformance> ComputeTopComps(IReadOnlyList<TftMatchResult> matches) =>
        matches
            .GroupBy(CompLabel)
            .Select(group => new CompPerformance
            {
                Comp = group.Key,
                Games = group.Count(),
                AveragePlacement = group.Average(m => m.Placement),
            })
            .OrderByDescending(c => c.Games)
            .ThenBy(c => c.AveragePlacement)
            .Take(TopCompsCount)
            .ToList();

    // A comp is identified by its leading one or two traits (already sorted highest-first).
    private static string CompLabel(TftMatchResult match)
    {
        var traitNames = match.Traits
            .Select(TraitName)
            .Where(name => name.Length > 0)
            .Take(2)
            .ToList();

        return traitNames.Count == 0 ? "Unknown" : string.Join(" + ", traitNames);
    }

    private static string TraitName(string trait)
    {
        var lastSpace = trait.LastIndexOf(' ');
        return lastSpace > 0 ? trait[..lastSpace].Trim() : trait.Trim();
    }

    // Compare the average placement of the older half against the newer half.
    // Lower placement is better, so a lower newer-half average means improvement.
    private static PlacementTrend ComputeTrend(IReadOnlyList<TftMatchResult> matches)
    {
        if (matches.Count < 2)
        {
            return PlacementTrend.Stable;
        }

        var ordered = matches.OrderBy(m => m.PlayedAt).ToList();
        var half = ordered.Count / 2;
        var olderAvg = ordered.Take(half).Average(m => m.Placement);
        var newerAvg = ordered.Skip(ordered.Count - half).Average(m => m.Placement);

        var delta = olderAvg - newerAvg;
        if (delta > TrendThreshold)
        {
            return PlacementTrend.Improving;
        }

        return delta < -TrendThreshold ? PlacementTrend.Declining : PlacementTrend.Stable;
    }

    private async Task PersistAsync(
        string? puuid,
        IReadOnlyList<TftMatchResult> matches,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(puuid))
        {
            return;
        }

        // Persistence is best-effort: a storage failure must not fail the analysis.
        try
        {
            await matchHistoryRepository.SaveAsync(puuid, matches, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist match history for analysis; continuing.");
        }
    }

    private async Task<IReadOnlyList<string>> RetrievePatchContextAsync(
        IReadOnlyList<CompPerformance> topComps,
        IReadOnlyList<TftMatchResult> matches,
        CancellationToken cancellationToken)
    {
        // RAG is best-effort: if embeddings or the vector store are unavailable, the
        // analysis still proceeds with no extra context.
        try
        {
            var query = BuildRetrievalQuery(topComps, matches);
            var embedding = await inference.EmbedAsync(query, cancellationToken);
            if (embedding.Count == 0)
            {
                return [];
            }

            return await vectorRepository.SearchAsync(embedding, PatchContextCount, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve patch context for analysis; continuing without it.");
            return [];
        }
    }

    private static string BuildRetrievalQuery(
        IReadOnlyList<CompPerformance> topComps,
        IReadOnlyList<TftMatchResult> matches)
    {
        var comps = topComps.Count > 0
            ? string.Join(", ", topComps.Select(c => c.Comp))
            : "various";
        var traits = matches
            .SelectMany(m => m.Traits.Select(TraitName))
            .Where(name => name.Length > 0)
            .Distinct()
            .Take(10);

        return $"Teamfight Tactics comps and meta for: {comps}. Traits: {string.Join(", ", traits)}.";
    }

    private async Task<MetaSnapshot?> RetrieveMetaAsync(CancellationToken cancellationToken)
    {
        // Meta grounding is best-effort: if the meta source is unavailable the analysis
        // still proceeds with no meta/fit section.
        try
        {
            return await metaProvider.GetCurrentMetaAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to retrieve current meta for analysis; continuing without it.");
            return null;
        }
    }

    private static string BuildPrompt(
        Stats stats,
        IReadOnlyList<CompPerformance> topComps,
        PlacementTrend trend,
        IReadOnlyList<TftMatchResult> matches,
        IReadOnlyList<string> patchContext,
        MetaSnapshot? meta)
    {
        var builder = new StringBuilder();
        var culture = CultureInfo.InvariantCulture;

        builder.AppendLine(culture, $"Matches analyzed: {matches.Count}");
        builder.AppendLine(culture, $"Average placement: {stats.AveragePlacement:F2}");
        builder.AppendLine(culture, $"Top-4 rate: {stats.Top4Rate:P0}");
        builder.AppendLine(culture, $"Win rate (1st): {stats.WinRate:P0}");
        builder.AppendLine(culture, $"Average level: {stats.AverageLevel:F1}");
        builder.AppendLine(culture, $"Placement trend: {trend}");

        builder.AppendLine();
        builder.AppendLine("Top comps (comp | games | avg placement):");
        foreach (var comp in topComps)
        {
            builder.AppendLine(culture, $"- {comp.Comp} | {comp.Games} | {comp.AveragePlacement:F2}");
        }

        AppendMetaSection(builder, culture, topComps, matches, meta);

        builder.AppendLine();
        builder.AppendLine("Per-match detail:");
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            builder.AppendLine(culture,
                $"{i + 1}. Placement {match.Placement}, level {match.Level}, " +
                $"gold left {match.GoldLeft}, last round {match.LastRound}, " +
                $"combat damage {match.TotalDamageToPlayers}. " +
                $"Traits: {Join(match.Traits)}. Augments: {Join(match.Augments)}. " +
                $"Units: {FormatUnits(match.Units)}.");
        }

        if (patchContext.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Relevant patch/meta context:");
            foreach (var snippet in patchContext)
            {
                builder.AppendLine(culture, $"- {snippet}");
            }
        }

        return builder.ToString();
    }

    // Renders the current meta as a compact list and a "meta fit" mapping of the
    // player's most-played comps onto it, naming the closest meta comp for each.
    // Mirrors the live coach's board-fit grounding: when nothing overlaps it tells
    // the model the comps are off-meta so it advises from first principles rather
    // than inventing a meta fit. Skipped entirely when no meta is available.
    private static void AppendMetaSection(
        StringBuilder builder,
        CultureInfo culture,
        IReadOnlyList<CompPerformance> topComps,
        IReadOnlyList<TftMatchResult> matches,
        MetaSnapshot? meta)
    {
        if (meta is null || meta.Comps.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine(culture,
            $"Current meta ({meta.Set}, patch {meta.Patch}) \u2014 the strongest comps right now:");
        foreach (var comp in meta.Comps.Take(MaxMetaComps))
        {
            var tier = string.IsNullOrWhiteSpace(comp.Tier) ? "" : $" [{comp.Tier.Trim()}]";
            var carries = comp.Carries.Count > 0 ? $" \u2014 carries {Join(comp.Carries)}" : "";
            builder.AppendLine(culture, $"- {comp.Name.Trim()}{tier}{carries}");
        }

        builder.AppendLine();
        builder.AppendLine("Meta fit (player's top comps mapped onto the current meta):");
        var anyFit = false;
        foreach (var comp in topComps)
        {
            var best = BestMetaFit(comp, matches, meta);
            if (best is { } fit)
            {
                anyFit = true;
                builder.AppendLine(culture,
                    $"- {comp.Comp} most closely matches {fit.Name.Trim()} ({fit.Overlap} shared carries/traits).");
            }
            else
            {
                builder.AppendLine(culture,
                    $"- {comp.Comp} matches no current meta comp \u2014 it is off-meta.");
            }
        }

        if (!anyFit)
        {
            builder.AppendLine(
                "None of the player's top comps map to the current meta \u2014 treat them as off-meta and " +
                "advise from first principles rather than inventing a meta fit.");
        }
    }

    // Finds the meta comp with the most overlap against a player comp. The player
    // comp's searchable text is its trait label plus the traits and units of every
    // match played under that label; overlap counts the meta comp's carries and
    // name words that appear in it (case-insensitive substring), mirroring the live
    // coach's CountCarryOverlap. Returns null when nothing overlaps.
    private static (string Name, int Overlap)? BestMetaFit(
        CompPerformance comp,
        IReadOnlyList<TftMatchResult> matches,
        MetaSnapshot meta)
    {
        var playerText = BuildCompText(comp, matches);
        if (playerText.Length == 0)
        {
            return null;
        }

        var best = meta.Comps
            .Select(m => (m.Name, Overlap: CountMetaOverlap(playerText, m)))
            .Where(x => x.Overlap > 0)
            .OrderByDescending(x => x.Overlap)
            .FirstOrDefault();

        return best.Overlap > 0 ? (best.Name, best.Overlap) : null;
    }

    // Aggregates a player comp's identity into one lower-cased haystack: its trait
    // label plus every trait name and unit name from the matches played under it.
    private static string BuildCompText(CompPerformance comp, IReadOnlyList<TftMatchResult> matches)
    {
        var tokens = new List<string> { comp.Comp };
        foreach (var match in matches.Where(m => CompLabel(m) == comp.Comp))
        {
            tokens.AddRange(match.Traits.Select(TraitName));
            tokens.AddRange(match.Units.Select(u => CleanItemName(u.Name)));
        }

        return string.Join(
            " | ",
            tokens.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.ToLowerInvariant()));
    }

    // Counts how many of a meta comp's carries and (longer) name words appear in the
    // player comp's text, ignoring short noise words.
    private static int CountMetaOverlap(string playerText, MetaComp comp)
    {
        var needles = comp.Carries
            .Concat(comp.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(n => n.Trim().Length > 2)
            .Select(n => n.Trim().ToLowerInvariant())
            .Distinct();

        return needles.Count(n => playerText.Contains(n, StringComparison.Ordinal));
    }

    private NarrativeResult ParseNarrative(string raw)
    {
        var json = ExtractJsonObject(raw);
        if (json is not null)
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<NarrativeDto>(json, JsonOptions);
                if (parsed is not null)
                {
                    return new NarrativeResult(
                        parsed.Strengths ?? [],
                        parsed.Weaknesses ?? [],
                        parsed.Suggestions ?? [],
                        (parsed.Narrative ?? string.Empty).Trim());
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "LM Studio analysis was not valid JSON; falling back to raw text.");
            }
        }

        // Fallback: use the raw model text as the narrative with no structured fields.
        return new NarrativeResult([], [], [], raw.Trim());
    }

    // Models sometimes wrap JSON in prose or code fences; extract the outermost object.
    private static string? ExtractJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        return start >= 0 && end > start ? raw[start..(end + 1)] : null;
    }

    private static string Join(IReadOnlyList<string> values) =>
        values.Count == 0 ? "none" : string.Join(", ", values);

    // Renders units as "Name 3* [Item, Item]", omitting star/items when unknown.
    private static string FormatUnits(IReadOnlyList<UnitDetail> units)
    {
        if (units.Count == 0)
        {
            return "none";
        }

        return string.Join(", ", units.Select(unit =>
        {
            var stars = unit.Tier > 0 ? $" {unit.Tier}*" : string.Empty;
            var items = unit.Items.Count > 0
                ? $" [{string.Join(", ", unit.Items.Select(CleanItemName))}]"
                : string.Empty;
            return $"{CleanItemName(unit.Name)}{stars}{items}";
        }));
    }

    // Strips Riot id prefixes ("TFT_Item_", "TFT9_Item_", "TFT17_") for readability/tokens.
    private static string CleanItemName(string raw)
    {
        var idx = raw.LastIndexOf('_');
        return idx >= 0 && idx < raw.Length - 1 ? raw[(idx + 1)..] : raw;
    }

    private readonly record struct Stats(
        double AveragePlacement,
        double Top4Rate,
        double WinRate,
        double AverageLevel);

    private readonly record struct NarrativeResult(
        IReadOnlyList<string> Strengths,
        IReadOnlyList<string> Weaknesses,
        IReadOnlyList<string> Suggestions,
        string Narrative);

    private sealed class NarrativeDto
    {
        public List<string>? Strengths { get; init; }
        public List<string>? Weaknesses { get; init; }
        public List<string>? Suggestions { get; init; }
        public string? Narrative { get; init; }
    }
}

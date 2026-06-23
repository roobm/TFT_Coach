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
    ILogger<AnalyzeMatchHistoryUseCase> logger)
{
    private const int TopCompsCount = 3;
    private const int PatchContextCount = 5;

    /// <summary>Minimum placement delta between halves before a trend is non-stable.</summary>
    private const double TrendThreshold = 0.5;

    private const string SystemPrompt =
        "You are a Challenger-level Teamfight Tactics coach writing a post-game review for a " +
        "student. You receive pre-computed statistics, the player's most-played comps, a placement " +
        "trend, per-match detail (traits, augments, units), and relevant patch/meta context.\n\n" +
        "Coaching lens — evaluate the player across these dimensions and reference them by name:\n" +
        "- Economy: gold management, interest breakpoints (10/20/30/40/50), and when to greed vs. spend.\n" +
        "- Tempo & leveling: when to push levels vs. hold, fast 8 vs. slow roll, and HP as a resource.\n" +
        "- Rolldown: hitting key units at the right level/stage and not over-rolling.\n" +
        "- Board strength: trait breakpoints, frontline/backline balance, and capped vs. uncapped boards.\n" +
        "- Itemization: item efficiency, BIS vs. forced items, and carry consistency.\n" +
        "- Comp flexibility: forcing one line vs. playing the board/augments/items they're given (pivots).\n" +
        "- Consistency: whether they fish for 1sts and bleed out, or play for stable top-4.\n\n" +
        "Ground every claim ONLY in the data provided — never invent champions, traits, items, or numbers. " +
        "Tie advice to the player's own patterns (e.g. cite their average placement, trend, or a recurring " +
        "comp/trait). Prefer specific, prioritised, actionable guidance over generic platitudes; lead each " +
        "suggestion with an action verb (e.g. Hold, Roll, Pivot, Slam, Scout, Level).\n\n" +
        "Respond with a single JSON object and nothing else, using exactly these keys: " +
        "\"strengths\" (array of strings), \"weaknesses\" (array of strings), " +
        "\"suggestions\" (array of strings, ordered most-impactful first), \"narrative\" (string, 3-5 " +
        "sentences summarising the player's play style and the single biggest lever to climb). " +
        "This is in-depth long-form analysis, not a one-line tip.";

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

        var userMessage = BuildPrompt(stats, topComps, trend, matches, patchContext);
        var raw = await inference.CompleteAsync(SystemPrompt, userMessage, cancellationToken);
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

    private static string BuildPrompt(
        Stats stats,
        IReadOnlyList<CompPerformance> topComps,
        PlacementTrend trend,
        IReadOnlyList<TftMatchResult> matches,
        IReadOnlyList<string> patchContext)
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

        builder.AppendLine();
        builder.AppendLine("Per-match detail:");
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            builder.AppendLine(culture,
                $"{i + 1}. Placement {match.Placement}, level {match.Level}. " +
                $"Traits: {Join(match.Traits)}. Augments: {Join(match.Augments)}. " +
                $"Units: {Join(match.Units)}.");
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

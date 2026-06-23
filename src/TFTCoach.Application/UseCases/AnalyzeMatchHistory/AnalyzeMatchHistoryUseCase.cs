using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using TFTCoach.Application.Interfaces;
using TFTCoach.Domain.Entities;

namespace TFTCoach.Application.UseCases.AnalyzeMatchHistory;

/// <summary>
/// Summarizes a player's recent matches into factual insights using local LM Studio.
/// </summary>
public sealed class AnalyzeMatchHistoryUseCase(
    ILMStudioClient inference,
    ILogger<AnalyzeMatchHistoryUseCase> logger)
{
    private const string SystemPrompt =
        "You are a Teamfight Tactics analyst. Given a player's recent match data, " +
        "produce 3 to 5 short, factual observations about their tendencies and results. " +
        "Base every observation ONLY on the data provided; never invent champions, traits, " +
        "or numbers. Use plain bullet points. Be concise and specific.";

    public async Task<AnalyzeMatchHistoryResponse> ExecuteAsync(
        AnalyzeMatchHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Matches.Count == 0)
        {
            return new AnalyzeMatchHistoryResponse
            {
                Insights = "No matches available to analyze.",
                MatchesAnalyzed = 0,
            };
        }

        var userMessage = BuildSummary(request.Matches);
        var insights = await inference.CompleteAsync(SystemPrompt, userMessage, cancellationToken);

        logger.LogInformation("Generated insights from {Count} matches", request.Matches.Count);

        return new AnalyzeMatchHistoryResponse
        {
            Insights = insights.Trim(),
            MatchesAnalyzed = request.Matches.Count,
        };
    }

    private static string BuildSummary(IReadOnlyList<TftMatchResult> matches)
    {
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Recent {matches.Count} matches:");

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            builder.AppendLine(CultureInfo.InvariantCulture,
                $"{i + 1}. Placement {match.Placement}, level {match.Level}. " +
                $"Traits: {Join(match.Traits)}. Augments: {Join(match.Augments)}. " +
                $"Units: {Join(match.Units)}.");
        }

        return builder.ToString();
    }

    private static string Join(IReadOnlyList<string> values) =>
        values.Count == 0 ? "none" : string.Join(", ", values);
}

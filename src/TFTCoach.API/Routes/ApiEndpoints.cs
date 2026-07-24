using TFTCoach.API.Hubs;
using TFTCoach.Application.Common;
using TFTCoach.Application.UseCases.AnalyzeMatchHistory;
using TFTCoach.Application.UseCases.DecodeTeamCode;
using TFTCoach.Application.UseCases.GetMatchHistory;
using TFTCoach.Application.UseCases.GetRanks;

namespace TFTCoach.API.Routes;

internal static class ApiEndpoints
{
    internal static void MapApiEndpoints(this WebApplication app)
    {
        app.MapGet("/", () => "TFT Coach API")
            .WithSummary("API health endpoint")
            .WithDescription("Returns a simple string confirming the TFT Coach API service is running.");

        // GET /matches?riotId=Name%23TAG&count=10&set=17
        app.MapGet("/matches", async (string riotId, int? count, int? set, GetMatchHistoryUseCase useCase, ILogger<Program> logger, CancellationToken ct) =>
        {
            if (!TryParseRiotId(riotId, out var gameName, out var tagLine))
            {
                return ToHttpResult(RequestResult<GetMatchHistoryResponse>.Fail(ApiErrors.Validation(
                    "validation.invalid_riot_id",
                    "riotId must be in the form gameName#tagLine.")));
            }

            try
            {
                var response = await useCase.ExecuteAsync(new GetMatchHistoryRequest
                {
                    GameName = gameName,
                    TagLine = tagLine,
                    Count = count ?? 10,
                    Set = set ?? 0,
                }, ct);

                return ToHttpResult(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load match history.");
                return ToHttpResult(RequestResult<GetMatchHistoryResponse>.Fail(ApiErrors.Unexpected(
                    "Failed to load match history.")));
            }
        })
            .WithSummary("Get TFT match history")
            .WithDescription("Fetches a player's recent TFT matches from Riot using a Riot ID formatted as gameName#tagLine. "
                + "Use count to limit how many matches are returned and set to filter by TFT set.");

        // POST /matches/analysis  { "matches": [...] }
        app.MapPost("/matches/analysis", async (AnalyzeMatchHistoryRequest request, AnalyzeMatchHistoryUseCase useCase, ILogger<Program> logger, CancellationToken ct) =>
        {
            try
            {
                var response = await useCase.ExecuteAsync(request, ct);
                return ToHttpResult(RequestResult<AnalyzeMatchHistoryResponse>.Ok(response));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to generate match analysis.");
                return ToHttpResult(RequestResult<AnalyzeMatchHistoryResponse>.Fail(ApiErrors.ExternalDependency(
                    "external.analysis_unavailable",
                    "Failed to generate match analysis.")));
            }
        })
            .WithSummary("Analyze match history with LM Studio")
            .WithDescription("Generates post-game coaching insights from provided match history data using the local LM Studio model.");

        // POST /teamcode  { "code": "0201d0...TFTSet17" }
        app.MapPost("/teamcode", async (DecodeTeamCodeRequest request, DecodeTeamCodeUseCase useCase, ILogger<Program> logger, CancellationToken ct) =>
        {
            try
            {
                var response = await useCase.ExecuteAsync(request, ct);
                return ToHttpResult(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to decode team code.");
                return ToHttpResult(RequestResult<DecodeTeamCodeResponse>.Fail(ApiErrors.Unexpected(
                    "Failed to decode team code.")));
            }
        })
            .WithSummary("Decode a TFT Team Planner code")
            .WithDescription("Decodes a TFT Team Planner share code into readable board units and positions.");

        // GET /ranks?riotId=Name%23TAG
        app.MapGet("/ranks", async (string riotId, GetRanksUseCase useCase, ILogger<Program> logger, CancellationToken ct) =>
        {
            if (!TryParseRiotId(riotId, out var gameName, out var tagLine))
            {
                return ToHttpResult(RequestResult<GetRanksResponse>.Fail(ApiErrors.Validation(
                    "validation.invalid_riot_id",
                    "riotId must be in the form gameName#tagLine.")));
            }

            try
            {
                var response = await useCase.ExecuteAsync(new GetRanksRequest
                {
                    GameName = gameName,
                    TagLine = tagLine,
                }, ct);

                return ToHttpResult(response);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load rank data.");
                return ToHttpResult(RequestResult<GetRanksResponse>.Fail(ApiErrors.Unexpected(
                    "Failed to load rank data.")));
            }
        })
            .WithSummary("Get player rank details")
            .WithDescription("Fetches current ranked TFT standings for a player by Riot ID (gameName#tagLine).");

        // SignalR hub for live, real-time coaching suggestions.
        app.MapHub<CoachHub>("/hubs/coach");
    }

    private static bool TryParseRiotId(string? riotId, out string gameName, out string tagLine)
    {
        gameName = string.Empty;
        tagLine = string.Empty;
        if (string.IsNullOrWhiteSpace(riotId))
        {
            return false;
        }

        var parts = riotId.Split('#', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
        {
            return false;
        }

        gameName = parts[0];
        tagLine = parts[1];
        return true;
    }

    private static IResult ToHttpResult<T>(RequestResult<T> result)
    {
        if (result.Success)
        {
            return Results.Ok(result);
        }

        var statusCode = result.Error?.Code switch
        {
            var code when code is not null && code.StartsWith("validation.", StringComparison.Ordinal) => StatusCodes.Status400BadRequest,
            var code when code is not null && code.StartsWith("not_found.", StringComparison.Ordinal) => StatusCodes.Status404NotFound,
            var code when code is not null && code.StartsWith("external.", StringComparison.Ordinal) => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Json(result, statusCode: statusCode);
    }
}

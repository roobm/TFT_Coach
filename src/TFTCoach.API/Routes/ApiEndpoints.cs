using TFTCoach.API.Hubs;
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
        app.MapGet("/matches", async (string riotId, int? count, int? set, GetMatchHistoryUseCase useCase, CancellationToken ct) =>
        {
            if (!TryParseRiotId(riotId, out var gameName, out var tagLine))
            {
                return Results.BadRequest("riotId must be in the form gameName#tagLine.");
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

                return Results.Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(ex.Message);
            }
        })
            .WithSummary("Get TFT match history")
            .WithDescription("Fetches a player's recent TFT matches from Riot using a Riot ID formatted as gameName#tagLine. "
                + "Use count to limit how many matches are returned and set to filter by TFT set.");

        // POST /matches/analysis  { "matches": [...] }
        app.MapPost("/matches/analysis", async (AnalyzeMatchHistoryRequest request, AnalyzeMatchHistoryUseCase useCase, CancellationToken ct) =>
        {
            var response = await useCase.ExecuteAsync(request, ct);
            return Results.Ok(response);
        })
            .WithSummary("Analyze match history with LM Studio")
            .WithDescription("Generates post-game coaching insights from provided match history data using the local LM Studio model.");

        // POST /teamcode  { "code": "0201d0...TFTSet17" }
        app.MapPost("/teamcode", async (DecodeTeamCodeRequest request, DecodeTeamCodeUseCase useCase, CancellationToken ct) =>
        {
            try
            {
                var response = await useCase.ExecuteAsync(request, ct);
                return Results.Ok(response);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        })
            .WithSummary("Decode a TFT Team Planner code")
            .WithDescription("Decodes a TFT Team Planner share code into readable board units and positions.");

        // GET /ranks?riotId=Name%23TAG
        app.MapGet("/ranks", async (string riotId, GetRanksUseCase useCase, CancellationToken ct) =>
        {
            if (!TryParseRiotId(riotId, out var gameName, out var tagLine))
            {
                return Results.BadRequest("riotId must be in the form gameName#tagLine.");
            }

            try
            {
                var response = await useCase.ExecuteAsync(new GetRanksRequest
                {
                    GameName = gameName,
                    TagLine = tagLine,
                }, ct);

                return Results.Ok(response);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(ex.Message);
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
}

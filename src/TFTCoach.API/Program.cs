using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TFTCoach.API.Hubs;
using TFTCoach.Application.Interfaces;
using TFTCoach.Application.UseCases.AnalyzeMatchHistory;
using TFTCoach.Application.UseCases.DecodeTeamCode;
using TFTCoach.Application.UseCases.GetMatchHistory;
using TFTCoach.Application.UseCases.GetRanks;
using TFTCoach.Application.UseCases.GetSuggestion;
using TFTCoach.Domain.Interfaces;
using TFTCoach.Infrastructure.InferenceService;
using TFTCoach.Infrastructure.Meta;
using TFTCoach.Infrastructure.Persistence;
using TFTCoach.Infrastructure.RiotApi;

var builder = WebApplication.CreateBuilder(args);

// Load .env into configuration so secrets stay out of source.
LoadDotEnv(Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".env"));

const string CorsPolicy = "frontend";
var frontendOrigin = Environment.GetEnvironmentVariable("FRONTEND_ORIGIN") ?? "http://localhost:5173";
var riotApiKey = Environment.GetEnvironmentVariable("RIOT_API_KEY") ?? string.Empty;
var riotRegion = Environment.GetEnvironmentVariable("RIOT_REGION") ?? "europe";
// League-V1 is platform-routed (euw1, na1, kr, ...), unlike the regional match endpoints.
var riotPlatform = Environment.GetEnvironmentVariable("RIOT_PLATFORM") ?? "euw1";
var lmStudioUrl = Environment.GetEnvironmentVariable("LM_STUDIO_URL") ?? "http://localhost:1234/v1";
var chatModel = Environment.GetEnvironmentVariable("CHAT_MODEL") ?? "local-model";
var embeddingModel = Environment.GetEnvironmentVariable("EMBEDDING_MODEL") ?? "local-embedding";
var supabaseDbUrl = Environment.GetEnvironmentVariable("SUPABASE_DB_URL")
    ?? "postgresql://postgres:postgres@127.0.0.1:54322/postgres";
var dbConnectionString = NpgsqlConnectionStringFactory.FromUrl(supabaseDbUrl);

// AllowCredentials is required for the SignalR browser client to negotiate a
// cross-origin connection; the origin is already pinned to the frontend.
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
    policy.WithOrigins(frontendOrigin).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// SignalR uses its own JSON protocol, so register the string-enum converter here
// too — otherwise SuggestionCategory would serialise as an integer to the client.
// Detailed errors are enabled: this is a local-first dev app with no remote clients.
builder.Services.AddSignalR(options => options.EnableDetailedErrors = true)
    .AddJsonProtocol(options =>
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Serialize enums (e.g. PlacementTrend) as their string names so the typed
// frontend contract receives "Stable"/"Improving"/"Declining", not integers.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Riot match client (regional routing host).
builder.Services.AddSingleton<RiotRateLimiter>();
builder.Services.AddTransient<RiotRateLimitHandler>();
builder.Services.AddHttpClient(nameof(RiotMatchClient), client =>
{
    client.BaseAddress = new Uri($"https://{riotRegion}.api.riotgames.com/");
    // Fetching many matches serialises behind Riot's rate limit (100 req / 2 min),
    // so a single request can sit in the limiter queue well past the default 100s.
    // Raise the timeout so the queue wait doesn't cancel the request.
    client.Timeout = TimeSpan.FromMinutes(10);
})
    .AddHttpMessageHandler<RiotRateLimitHandler>();
builder.Services.AddScoped<IRiotMatchClient>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(RiotMatchClient));
    return new RiotMatchClient(http, riotApiKey, sp.GetRequiredService<ILogger<RiotMatchClient>>());
});

// Riot league client (platform routing host) for ranked standing.
builder.Services.AddHttpClient(nameof(RiotLeagueClient), client =>
{
    client.BaseAddress = new Uri($"https://{riotPlatform}.api.riotgames.com/");
    client.Timeout = TimeSpan.FromMinutes(2);
})
    .AddHttpMessageHandler<RiotRateLimitHandler>();
builder.Services.AddScoped<IRiotLeagueClient>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(RiotLeagueClient));
    return new RiotLeagueClient(http, riotApiKey, sp.GetRequiredService<ILogger<RiotLeagueClient>>());
});

// Local LM Studio client.
builder.Services.AddHttpClient(nameof(LMStudioClient), client =>
{
    client.BaseAddress = new Uri(lmStudioUrl.TrimEnd('/') + "/");
    // In-depth analysis over many matches can take a while on local hardware, so
    // raise the timeout well past the default 100s to avoid cancelling inference.
    client.Timeout = TimeSpan.FromMinutes(10);
});
builder.Services.AddScoped<ILMStudioClient>(sp =>
{
    var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(LMStudioClient));
    return new LMStudioClient(http, embeddingModel, chatModel, sp.GetRequiredService<ILogger<LMStudioClient>>());
});

// Supabase persistence (pgvector patch store + match history) via Npgsql.
builder.Services.AddScoped<IVectorRepository>(sp =>
    new SupabaseVectorRepository(dbConnectionString, sp.GetRequiredService<ILogger<SupabaseVectorRepository>>()));
builder.Services.AddScoped<IMatchHistoryRepository>(sp =>
    new SupabaseMatchHistoryRepository(dbConnectionString, sp.GetRequiredService<ILogger<SupabaseMatchHistoryRepository>>()));
builder.Services.AddScoped<IRankHistoryRepository>(sp =>
    new SupabaseRankHistoryRepository(dbConnectionString, sp.GetRequiredService<ILogger<SupabaseRankHistoryRepository>>()));

builder.Services.AddScoped<GetMatchHistoryUseCase>();
builder.Services.AddScoped<AnalyzeMatchHistoryUseCase>();
builder.Services.AddScoped<GetRanksUseCase>();
builder.Services.AddScoped<GetSuggestionUseCase>();
builder.Services.AddScoped<DecodeTeamCodeUseCase>();

// Decodes shareable TFT Team Planner codes into board units using Riot's official
// team-planner static data (Community Dragon). Cached per set inside the provider.
builder.Services.AddScoped<ITftTeamPlanner>(sp =>
    new CommunityDragonTeamPlanner(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
        sp.GetRequiredService<ILogger<CommunityDragonTeamPlanner>>()));

// Current meta for grounding live coaching. Primary source is the live Riot
// ladder (RiotMetaProvider, computed from top KR Challenger matches); a curated
// patch-tagged JSON file is the resilient fallback when Riot is unavailable.
var metaFilePath = Environment.GetEnvironmentVariable("META_FILE_PATH")
    ?? Path.Combine(AppContext.BaseDirectory, "data", "meta", "current.json");

// The meta is sampled from a fixed high-elo ladder (KR by default) regardless of
// the player's own region, so it uses its own routing hosts.
var metaPlatform = Environment.GetEnvironmentVariable("META_PLATFORM") ?? "kr";
var metaRegion = Environment.GetEnvironmentVariable("META_REGION") ?? "asia";

builder.Services.AddHttpClient("RiotMetaLeagueClient", client =>
{
    client.BaseAddress = new Uri($"https://{metaPlatform}.api.riotgames.com/");
    client.Timeout = TimeSpan.FromMinutes(2);
})
    .AddHttpMessageHandler<RiotRateLimitHandler>();
builder.Services.AddHttpClient("RiotMetaMatchClient", client =>
{
    client.BaseAddress = new Uri($"https://{metaRegion}.api.riotgames.com/");
    client.Timeout = TimeSpan.FromMinutes(10);
})
    .AddHttpMessageHandler<RiotRateLimitHandler>();

builder.Services.AddSingleton<IMetaProvider>(sp =>
{
    var fallback = new FileMetaProvider(metaFilePath, sp.GetRequiredService<ILogger<FileMetaProvider>>());

    if (string.IsNullOrWhiteSpace(riotApiKey))
    {
        // No Riot key: serve the curated file only.
        return fallback;
    }

    var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
    var leagueClient = new RiotLeagueClient(
        httpFactory.CreateClient("RiotMetaLeagueClient"), riotApiKey, sp.GetRequiredService<ILogger<RiotLeagueClient>>());
    var matchClient = new RiotMatchClient(
        httpFactory.CreateClient("RiotMetaMatchClient"), riotApiKey, sp.GetRequiredService<ILogger<RiotMatchClient>>());

    // Sample size is env-tunable. Bigger = more accurate tiers but a longer warm-up,
    // because each Riot call is throttled to the dev-key limit (100 req / 2 min).
    var defaults = new RiotMetaOptions();
    var metaOptions = new RiotMetaOptions
    {
        MaxPlayers = EnvInt("META_MAX_PLAYERS", defaults.MaxPlayers),
        MatchesPerPlayer = EnvInt("META_MATCHES_PER_PLAYER", defaults.MatchesPerPlayer),
        MinSampleSize = EnvInt("META_MIN_SAMPLE", defaults.MinSampleSize),
        TopComps = EnvInt("META_TOP_COMPS", defaults.TopComps),
    };

    // Persist computed snapshots (grouped by set) so a restart serves the last
    // known meta instantly instead of recomputing from the ladder. Best-effort:
    // the provider degrades gracefully if the DB is unavailable.
    var metaCache = new SupabaseMetaCacheRepository(
        dbConnectionString, sp.GetRequiredService<ILogger<SupabaseMetaCacheRepository>>());

    // Resolves raw Riot ids (TFT_Item_*, TFT14_*) to in-game display names so comps
    // read correctly. Reuses the Community Dragon static-data file; degrades to an
    // id-cleanup fallback if it can't be fetched.
    var staticData = new CommunityDragonStaticData(
        httpFactory.CreateClient(), sp.GetRequiredService<ILogger<CommunityDragonStaticData>>());

    return new RiotMetaProvider(
        leagueClient, matchClient, fallback, sp.GetRequiredService<ILogger<RiotMetaProvider>>(),
        metaOptions, metaCache, staticData);
});

var app = builder.Build();
app.UseCors(CorsPolicy);

app.MapGet("/", () => "TFT Coach API");

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
});

// POST /matches/analysis  { "matches": [...] }
app.MapPost("/matches/analysis", async (AnalyzeMatchHistoryRequest request, AnalyzeMatchHistoryUseCase useCase, CancellationToken ct) =>
{
    var response = await useCase.ExecuteAsync(request, ct);
    return Results.Ok(response);
});

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
});

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
});

// SignalR hub for live, real-time coaching suggestions.
app.MapHub<CoachHub>("/hubs/coach");

app.Run();

static int EnvInt(string name, int fallback)
{
    var raw = Environment.GetEnvironmentVariable(name);
    return int.TryParse(raw, out var value) && value > 0 ? value : fallback;
}

static bool TryParseRiotId(string? riotId, out string gameName, out string tagLine){
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

static void LoadDotEnv(string path)
{
    if (!File.Exists(path))
    {
        return;
    }

    foreach (var rawLine in File.ReadAllLines(path))
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
        {
            continue;
        }

        var separator = line.IndexOf('=');
        if (separator <= 0)
        {
            continue;
        }

        var key = line[..separator].Trim();
        var value = line[(separator + 1)..].Trim().Trim('"');
        if (Environment.GetEnvironmentVariable(key) is null)
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

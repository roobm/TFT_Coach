using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TFTCoach.Application.Interfaces;
using TFTCoach.Application.UseCases.AnalyzeMatchHistory;
using TFTCoach.Application.UseCases.GetMatchHistory;
using TFTCoach.Domain.Interfaces;
using TFTCoach.Infrastructure.InferenceService;
using TFTCoach.Infrastructure.Persistence;
using TFTCoach.Infrastructure.RiotApi;

var builder = WebApplication.CreateBuilder(args);

// Load .env into configuration so secrets stay out of source.
LoadDotEnv(Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".env"));

const string CorsPolicy = "frontend";
var frontendOrigin = Environment.GetEnvironmentVariable("FRONTEND_ORIGIN") ?? "http://localhost:5173";
var riotApiKey = Environment.GetEnvironmentVariable("RIOT_API_KEY") ?? string.Empty;
var riotRegion = Environment.GetEnvironmentVariable("RIOT_REGION") ?? "europe";
var lmStudioUrl = Environment.GetEnvironmentVariable("LM_STUDIO_URL") ?? "http://localhost:1234/v1";
var chatModel = Environment.GetEnvironmentVariable("CHAT_MODEL") ?? "local-model";
var embeddingModel = Environment.GetEnvironmentVariable("EMBEDDING_MODEL") ?? "local-embedding";
var supabaseDbUrl = Environment.GetEnvironmentVariable("SUPABASE_DB_URL")
    ?? "postgresql://postgres:postgres@127.0.0.1:54322/postgres";
var dbConnectionString = NpgsqlConnectionStringFactory.FromUrl(supabaseDbUrl);

builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
    policy.WithOrigins(frontendOrigin).AllowAnyHeader().AllowAnyMethod()));

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

builder.Services.AddScoped<GetMatchHistoryUseCase>();
builder.Services.AddScoped<AnalyzeMatchHistoryUseCase>();

var app = builder.Build();
app.UseCors(CorsPolicy);

app.MapGet("/", () => "TFT Coach API");

// GET /matches?riotId=Name%23TAG&count=10
app.MapGet("/matches", async (string riotId, int? count, GetMatchHistoryUseCase useCase, CancellationToken ct) =>
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

app.Run();

static bool TryParseRiotId(string? riotId, out string gameName, out string tagLine)
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

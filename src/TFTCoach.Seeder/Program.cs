using Microsoft.Extensions.Logging;
using TFTCoach.Application.UseCases.SeedPatchData;
using TFTCoach.Infrastructure.InferenceService;
using TFTCoach.Infrastructure.Persistence;
using TFTCoach.Infrastructure.RiotApi;
using TFTCoach.Seeder;

// Usage: dotnet run --project src/TFTCoach.Seeder -- --patch 14.1
//   --patch <version>           Patch tag to seed (required).
//   --embedding-model <name>    LM Studio embedding model id (env EMBEDDING_MODEL).
//   --embedding-dim <n>         Expected embedding dimension (env EMBEDDING_DIM, default 1536).

EnvFile.LoadInto(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

var arguments = CliArgs.Parse(args);
var patch = arguments.GetValueOrDefault("patch");
if (string.IsNullOrWhiteSpace(patch))
{
    Console.Error.WriteLine("Error: --patch <version> is required, e.g. --patch 14.1");
    return 1;
}

var lmStudioUrl = Environment.GetEnvironmentVariable("LM_STUDIO_URL") ?? "http://localhost:1234/v1";
var dbUrl = Environment.GetEnvironmentVariable("SUPABASE_DB_URL")
    ?? "postgresql://postgres:postgres@127.0.0.1:54322/postgres";
var embeddingModel = arguments.GetValueOrDefault("embedding-model")
    ?? Environment.GetEnvironmentVariable("EMBEDDING_MODEL")
    ?? "text-embedding-nomic-embed-text-v1.5";
var embeddingDim = int.TryParse(
    arguments.GetValueOrDefault("embedding-dim") ?? Environment.GetEnvironmentVariable("EMBEDDING_DIM"),
    out var dim)
    ? dim
    : 1536;

using var loggerFactory = LoggerFactory.Create(builder => builder
    .SetMinimumLevel(LogLevel.Information)
    .AddSimpleConsole(options => options.SingleLine = true));

using var dataHttp = new HttpClient();
using var lmHttp = new HttpClient { BaseAddress = new Uri(lmStudioUrl.TrimEnd('/') + "/") };

var contentProvider = new DataDragonService(dataHttp, loggerFactory.CreateLogger<DataDragonService>());
var inference = new LMStudioClient(lmHttp, embeddingModel, chatModel: embeddingModel, loggerFactory.CreateLogger<LMStudioClient>());
var repository = new SupabaseVectorRepository(
    NpgsqlConnectionString.FromUrl(dbUrl),
    loggerFactory.CreateLogger<SupabaseVectorRepository>());

var useCase = new SeedPatchDataUseCase(
    contentProvider,
    inference,
    repository,
    loggerFactory.CreateLogger<SeedPatchDataUseCase>());

try
{
    var response = await useCase.ExecuteAsync(new SeedPatchDataRequest
    {
        Patch = patch,
        ExpectedEmbeddingDimension = embeddingDim,
    });

    Console.WriteLine($"Seeded {response.DocumentsSeeded} documents for patch {response.Patch}.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Seeding failed: {ex.Message}");
    return 1;
}

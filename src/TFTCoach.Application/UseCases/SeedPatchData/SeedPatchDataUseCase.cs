using Microsoft.Extensions.Logging;
using TFTCoach.Application.Interfaces;
using TFTCoach.Domain.Entities;
using TFTCoach.Domain.Interfaces;

namespace TFTCoach.Application.UseCases.SeedPatchData;

/// <summary>
/// Fetches patch content, embeds each chunk via local LM Studio, and upserts the
/// resulting vectors into the store. Touches only <c>tft_patch_data</c>.
/// </summary>
public sealed class SeedPatchDataUseCase(
    IPatchContentProvider contentProvider,
    ILMStudioClient inference,
    IVectorRepository vectorRepository,
    ILogger<SeedPatchDataUseCase> logger)
{
    public async Task<SeedPatchDataResponse> ExecuteAsync(
        SeedPatchDataRequest request,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Seeding patch data for patch {Patch}", request.Patch);

        var documents = await contentProvider.GetDocumentsAsync(request.Patch, cancellationToken);
        logger.LogInformation("Fetched {Count} content chunks for patch {Patch}", documents.Count, request.Patch);

        var embedded = new List<PatchDocument>(documents.Count);
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var embedding = await inference.EmbedAsync(document.Content, cancellationToken);

            if (embedding.Count != request.ExpectedEmbeddingDimension)
            {
                throw new InvalidOperationException(
                    $"Embedding dimension mismatch for patch {request.Patch}: model returned " +
                    $"{embedding.Count}, expected {request.ExpectedEmbeddingDimension}. " +
                    "Load an embedding model with the correct dimension or adjust the schema.");
            }

            embedded.Add(new PatchDocument
            {
                Patch = document.Patch,
                Category = document.Category,
                Content = document.Content,
                Embedding = embedding,
            });
        }

        var written = await vectorRepository.UpsertAsync(embedded, cancellationToken);
        logger.LogInformation("Seeded {Count} documents for patch {Patch}", written, request.Patch);

        return new SeedPatchDataResponse
        {
            Patch = request.Patch,
            DocumentsSeeded = written,
        };
    }
}

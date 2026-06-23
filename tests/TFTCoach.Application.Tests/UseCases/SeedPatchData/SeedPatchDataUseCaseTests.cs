using Microsoft.Extensions.Logging.Abstractions;
using TFTCoach.Application.Interfaces;
using TFTCoach.Application.UseCases.SeedPatchData;
using TFTCoach.Domain.Entities;
using TFTCoach.Domain.Interfaces;

namespace TFTCoach.Application.Tests.UseCases.SeedPatchData;

public class SeedPatchDataUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldEmbedAndUpsertAllDocuments_WhenDimensionsMatch()
    {
        // Arrange
        var documents = new List<PatchDocument>
        {
            new() { Patch = "14.1", Category = "champion", Content = "Champion: Ahri." },
            new() { Patch = "14.1", Category = "trait", Content = "Trait: Sorcerer." },
        };
        var provider = new FakeContentProvider(documents);
        var inference = new FakeInferenceClient(dimension: 3);
        var repository = new RecordingVectorRepository();
        var useCase = new SeedPatchDataUseCase(provider, inference, repository, NullLogger<SeedPatchDataUseCase>.Instance);

        // Act
        var response = await useCase.ExecuteAsync(new SeedPatchDataRequest
        {
            Patch = "14.1",
            ExpectedEmbeddingDimension = 3,
        });

        // Assert
        Assert.Equal(2, response.DocumentsSeeded);
        Assert.Equal(2, repository.Written.Count);
        Assert.All(repository.Written, d => Assert.Equal(3, d.Embedding.Count));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrow_WhenEmbeddingDimensionMismatches()
    {
        // Arrange
        var documents = new List<PatchDocument>
        {
            new() { Patch = "14.1", Category = "item", Content = "Item: Deathblade." },
        };
        var useCase = new SeedPatchDataUseCase(
            new FakeContentProvider(documents),
            new FakeInferenceClient(dimension: 2),
            new RecordingVectorRepository(),
            NullLogger<SeedPatchDataUseCase>.Instance);

        // Act + Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync(new SeedPatchDataRequest
        {
            Patch = "14.1",
            ExpectedEmbeddingDimension = 1536,
        }));
    }

    private sealed class FakeContentProvider(IReadOnlyList<PatchDocument> documents) : IPatchContentProvider
    {
        public Task<IReadOnlyList<PatchDocument>> GetDocumentsAsync(string patch, CancellationToken cancellationToken = default)
            => Task.FromResult(documents);
    }

    private sealed class FakeInferenceClient(int dimension) : ILMStudioClient
    {
        public Task<IReadOnlyList<float>> EmbedAsync(string text, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<float>>(Enumerable.Repeat(0.1f, dimension).ToList());

        public Task<string> CompleteAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);
    }

    private sealed class RecordingVectorRepository : IVectorRepository
    {
        public List<PatchDocument> Written { get; } = [];

        public Task<int> UpsertAsync(IReadOnlyList<PatchDocument> documents, CancellationToken cancellationToken = default)
        {
            Written.AddRange(documents);
            return Task.FromResult(documents.Count);
        }
    }
}

using TFTCoach.Domain.Entities;

namespace TFTCoach.Domain.Interfaces;

/// <summary>
/// Stores embedded patch documents for similarity search.
/// </summary>
public interface IVectorRepository
{
    /// <summary>
    /// Inserts the given embedded documents into the vector store.
    /// </summary>
    /// <returns>The number of documents written.</returns>
    Task<int> UpsertAsync(IReadOnlyList<PatchDocument> documents, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the <paramref name="topK"/> stored documents whose embeddings are most
    /// similar (cosine distance) to <paramref name="embedding"/>, as their text content.
    /// </summary>
    Task<IReadOnlyList<string>> SearchAsync(
        IReadOnlyList<float> embedding,
        int topK,
        CancellationToken cancellationToken = default);
}

namespace TFTCoach.Domain.Entities;

/// <summary>
/// A single chunk of TFT knowledge prepared for vector search.
/// Maps to a row in the <c>tft_patch_data</c> table.
/// </summary>
public sealed class PatchDocument
{
    public required string Patch { get; init; }

    /// <summary>One of: champion | item | augment | trait | patch_note.</summary>
    public required string Category { get; init; }

    public required string Content { get; init; }

    /// <summary>
    /// Embedding vector for <see cref="Content"/>. Empty until produced by the
    /// inference client; must match the database vector dimension before upsert.
    /// </summary>
    public IReadOnlyList<float> Embedding { get; init; } = [];
}

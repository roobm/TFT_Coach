namespace TFTCoach.Application.UseCases.SeedPatchData;

/// <summary>
/// Request to (re-)seed vector data for a single patch.
/// </summary>
public sealed class SeedPatchDataRequest
{
    /// <summary>Patch version to seed, e.g. <c>14.1</c>.</summary>
    public required string Patch { get; init; }

    /// <summary>
    /// Expected embedding dimension. Documents whose embedding length differs
    /// are rejected so a model/schema mismatch fails fast.
    /// </summary>
    public int ExpectedEmbeddingDimension { get; init; } = 1536;
}

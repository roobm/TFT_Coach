namespace TFTCoach.Application.UseCases.SeedPatchData;

/// <summary>
/// Result of a seed run.
/// </summary>
public sealed class SeedPatchDataResponse
{
    public required string Patch { get; init; }

    /// <summary>Number of documents embedded and written to the vector store.</summary>
    public required int DocumentsSeeded { get; init; }
}

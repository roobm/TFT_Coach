using TFTCoach.Domain.Entities;

namespace TFTCoach.Application.Interfaces;

/// <summary>
/// Supplies un-embedded patch documents (chunked champion/item/trait/patch-note
/// content) for a given patch version.
/// </summary>
public interface IPatchContentProvider
{
    Task<IReadOnlyList<PatchDocument>> GetDocumentsAsync(string patch, CancellationToken cancellationToken = default);
}

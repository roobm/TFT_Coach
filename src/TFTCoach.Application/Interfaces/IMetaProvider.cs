using TFTCoach.Domain.Entities;

namespace TFTCoach.Application.Interfaces;

/// <summary>
/// Supplies the current TFT meta (the strongest comps for the active set +
/// patch) used to ground live coaching. Implementations may read a curated
/// local file or, later, a community data source. Returning <c>null</c> means
/// no meta is available and the coach falls back to first-principles advice.
/// </summary>
public interface IMetaProvider
{
    Task<MetaSnapshot?> GetCurrentMetaAsync(CancellationToken cancellationToken = default);
}

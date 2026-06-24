using Microsoft.AspNetCore.SignalR;
using TFTCoach.Application.Interfaces;
using TFTCoach.Application.UseCases.GetSuggestion;

namespace TFTCoach.API.Hubs;

/// <summary>
/// Lightweight view of the active meta sent to the client so it can show which
/// set + patch the coach is grounded on.
/// </summary>
public sealed record MetaInfo(string Set, string Patch, IReadOnlyList<string> Comps);

/// <summary>One meta composition with the detail the Comps page renders.</summary>
public sealed record MetaCompInfo(
    string Name,
    string Tier,
    string Playstyle,
    IReadOnlyList<string> Carries,
    IReadOnlyList<string> KeyItems,
    string? Notes);

/// <summary>The full active meta (set, patch, and every comp) for the Comps page.</summary>
public sealed record MetaDetail(string Set, string Patch, IReadOnlyList<MetaCompInfo> Comps);

/// <summary>
/// Real-time hub for live coaching. The client invokes <see cref="GetSuggestion"/>
/// with the current board snapshot and awaits a single tagged suggestion in
/// return. Each call is stateless — the hub keeps no conversational history.
/// </summary>
public sealed class CoachHub(
    GetSuggestionUseCase useCase,
    IMetaProvider metaProvider,
    ILogger<CoachHub> logger) : Hub
{
    /// <summary>
    /// Produces one coaching suggestion for the supplied game state and returns
    /// it to the calling client.
    /// </summary>
    public async Task<GetSuggestionResponse> GetSuggestion(GetSuggestionRequest request)
    {
        try
        {
            return await useCase.ExecuteAsync(request, Context.ConnectionAborted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate a live coaching suggestion.");
            throw new HubException("Failed to generate a suggestion. Is LM Studio running?");
        }
    }

    /// <summary>
    /// Returns the active meta (set, patch, comp names) the coach is grounded on,
    /// or <c>null</c> if no meta is configured.
    /// </summary>
    public async Task<MetaInfo?> GetMeta()
    {
        try
        {
            var meta = await metaProvider.GetCurrentMetaAsync(Context.ConnectionAborted);
            if (meta is null)
            {
                return null;
            }

            return new MetaInfo(meta.Set, meta.Patch, meta.Comps.Select(c => c.Name).ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load the current meta.");
            throw new HubException("Failed to load the current meta.");
        }
    }

    /// <summary>
    /// Returns the full current meta — set, patch, and every comp with its tier,
    /// carries, key items and notes — for the Comps page. <c>null</c> when no meta
    /// is configured.
    /// </summary>
    public async Task<MetaDetail?> GetMetaComps()
    {
        try
        {
            var meta = await metaProvider.GetCurrentMetaAsync(Context.ConnectionAborted);
            if (meta is null)
            {
                return null;
            }

            var comps = meta.Comps
                .Select(c => new MetaCompInfo(c.Name, c.Tier, c.Playstyle, c.Carries, c.KeyItems, c.Notes))
                .ToList();
            return new MetaDetail(meta.Set, meta.Patch, comps);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load the current meta comps.");
            throw new HubException("Failed to load the current meta.");
        }
    }
}

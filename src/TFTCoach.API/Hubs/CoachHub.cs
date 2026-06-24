using Microsoft.AspNetCore.SignalR;
using TFTCoach.Application.UseCases.GetSuggestion;

namespace TFTCoach.API.Hubs;

/// <summary>
/// Real-time hub for live coaching. The client invokes <see cref="GetSuggestion"/>
/// with the current board snapshot and awaits a single tagged suggestion in
/// return. Each call is stateless — the hub keeps no conversational history.
/// </summary>
public sealed class CoachHub(GetSuggestionUseCase useCase, ILogger<CoachHub> logger) : Hub
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
}

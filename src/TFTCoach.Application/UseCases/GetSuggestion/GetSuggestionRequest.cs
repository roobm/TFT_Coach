using TFTCoach.Domain.Entities;

namespace TFTCoach.Application.UseCases.GetSuggestion;

/// <summary>
/// Request for a single live coaching suggestion, built from one point-in-time
/// snapshot of the player's board. Stateless — no conversational history.
/// </summary>
public sealed class GetSuggestionRequest
{
    public required GameState State { get; init; }
}

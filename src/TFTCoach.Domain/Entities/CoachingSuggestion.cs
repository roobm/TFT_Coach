namespace TFTCoach.Domain.Entities;

/// <summary>
/// A single live coaching suggestion: one category and a short piece of advice
/// (at most two sentences, leading with an action verb). Produced statelessly
/// from one <see cref="GameState"/> snapshot.
/// </summary>
public sealed class CoachingSuggestion
{
    /// <summary>Which coaching dimension this suggestion addresses.</summary>
    public required SuggestionCategory Category { get; init; }

    /// <summary>The advice text, without the leading <c>[TAG]</c>.</summary>
    public required string Text { get; init; }
}

using TFTCoach.Domain.Entities;

namespace TFTCoach.Application.UseCases.GetSuggestion;

/// <summary>
/// A single live coaching suggestion: the category it addresses and the advice
/// text (at most two sentences, leading with an action verb, no leading tag).
/// </summary>
public sealed class GetSuggestionResponse
{
    public required SuggestionCategory Category { get; init; }

    public required string Text { get; init; }
}

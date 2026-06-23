using System.Text;

namespace TFTCoach.Infrastructure.RiotApi;

/// <summary>
/// Turns raw TFT entities into compact, human-readable text chunks suitable for
/// embedding. Long descriptions are trimmed of redundant whitespace.
/// </summary>
internal static class PatchNoteChunker
{
    public static string Champion(string name, int cost, IReadOnlyList<string> traits, string? abilityName, string? abilityDescription)
    {
        var builder = new StringBuilder();
        builder.Append($"Champion: {name}. Cost: {cost}.");
        if (traits.Count > 0)
        {
            builder.Append($" Traits: {string.Join(", ", traits)}.");
        }

        if (!string.IsNullOrWhiteSpace(abilityName))
        {
            builder.Append($" Ability: {abilityName}.");
        }

        if (!string.IsNullOrWhiteSpace(abilityDescription))
        {
            builder.Append($" {Normalize(abilityDescription)}");
        }

        return builder.ToString().Trim();
    }

    public static string Trait(string name, string? description)
    {
        var text = $"Trait: {name}.";
        if (!string.IsNullOrWhiteSpace(description))
        {
            text += $" {Normalize(description)}";
        }

        return text.Trim();
    }

    public static string Item(string name, string? description)
    {
        var text = $"Item: {name}.";
        if (!string.IsNullOrWhiteSpace(description))
        {
            text += $" {Normalize(description)}";
        }

        return text.Trim();
    }

    /// <summary>Collapses runs of whitespace and strips simple markup tokens.</summary>
    private static string Normalize(string raw)
    {
        var withoutTags = raw
            .Replace("<br>", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("\n", " ")
            .Replace("\r", " ");

        return string.Join(' ', withoutTags.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}

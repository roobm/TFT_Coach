namespace TFTCoach.Domain.Entities;

/// <summary>
/// A snapshot of the player's current in-game situation, supplied by the player
/// on the live-coach page. Every field is a point-in-time fact about the board;
/// suggestions are produced statelessly from a single snapshot.
/// </summary>
public sealed class GameState
{
    /// <summary>Current stage and round, e.g. <c>3-2</c>. Empty when unknown.</summary>
    public string Stage { get; init; } = string.Empty;

    /// <summary>Gold currently in hand.</summary>
    public int Gold { get; init; }

    /// <summary>Player level (number of board slots).</summary>
    public int Level { get; init; }

    /// <summary>Remaining health (0..100).</summary>
    public int Health { get; init; }

    /// <summary>
    /// Current win/loss streak: positive for a win streak, negative for a loss
    /// streak, zero when not on a streak.
    /// </summary>
    public int Streak { get; init; }

    /// <summary>Units currently on the board (free-form labels, optionally with star/items).</summary>
    public IReadOnlyList<string> Units { get; init; } = [];

    /// <summary>Items or components available (on the bench or unequipped).</summary>
    public IReadOnlyList<string> Items { get; init; } = [];

    /// <summary>Optional free-text notes from the player (e.g. what they're contesting).</summary>
    public string? Notes { get; init; }
}

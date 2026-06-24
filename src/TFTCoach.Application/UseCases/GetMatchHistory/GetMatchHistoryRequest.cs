namespace TFTCoach.Application.UseCases.GetMatchHistory;

/// <summary>
/// Request for a player's recent TFT matches by Riot ID.
/// </summary>
public sealed class GetMatchHistoryRequest
{
    /// <summary>Riot ID game name (the part before <c>#</c>).</summary>
    public required string GameName { get; init; }

    /// <summary>Riot ID tag line (the part after <c>#</c>).</summary>
    public required string TagLine { get; init; }

    /// <summary>Number of recent matches to fetch (clamped 1..200).</summary>
    public int Count { get; init; } = 10;

    /// <summary>
    /// Target TFT set (season) to return. 0 (default) means the current set.
    /// A specific older set is fetched on demand from Riot (rate-limited).
    /// </summary>
    public int Set { get; init; }
}

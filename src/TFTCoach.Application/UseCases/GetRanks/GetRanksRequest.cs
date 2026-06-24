namespace TFTCoach.Application.UseCases.GetRanks;

/// <summary>
/// Request for a player's ranked standing by Riot ID.
/// </summary>
public sealed class GetRanksRequest
{
    /// <summary>Riot ID game name (the part before <c>#</c>).</summary>
    public required string GameName { get; init; }

    /// <summary>Riot ID tag line (the part after <c>#</c>).</summary>
    public required string TagLine { get; init; }
}

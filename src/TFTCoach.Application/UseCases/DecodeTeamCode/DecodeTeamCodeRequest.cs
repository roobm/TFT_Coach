namespace TFTCoach.Application.UseCases.DecodeTeamCode;

/// <summary>
/// Request to decode a TFT Team Planner code (e.g.
/// <c>0201d00d02103903b03801305003a000TFTSet17</c>) into its champions.
/// </summary>
public sealed class DecodeTeamCodeRequest
{
    public required string Code { get; init; }
}

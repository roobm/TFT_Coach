namespace TFTCoach.Application.UseCases.DecodeTeamCode;

/// <summary>One decoded board unit.</summary>
public sealed class DecodedUnit
{
    public required string Name { get; init; }

    /// <summary>Gold cost / tier (1–5).</summary>
    public required int Cost { get; init; }
}

/// <summary>
/// Result of decoding a TFT Team Planner code into its champions.
/// </summary>
public sealed class DecodeTeamCodeResponse
{
    /// <summary>Set identifier parsed from the code, e.g. <c>TFTSet17</c>.</summary>
    public required string Set { get; init; }

    /// <summary>Decoded champions, in the order they appear in the code.</summary>
    public required IReadOnlyList<DecodedUnit> Units { get; init; }
}

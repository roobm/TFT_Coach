using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TFTCoach.Application.Common;
using TFTCoach.Application.Interfaces;

namespace TFTCoach.Application.UseCases.DecodeTeamCode;

/// <summary>
/// Decodes a TFT Team Planner code into its champions. The parsing (version,
/// hex chunking, set suffix) is pure; champion lookup is delegated to
/// <see cref="ITftTeamPlanner"/>, which is backed by official static game data.
/// </summary>
public sealed partial class DecodeTeamCodeUseCase(
    ITftTeamPlanner teamPlanner,
    ILogger<DecodeTeamCodeUseCase> logger)
{
    // 0201d00d...TFTSet17  ->  prefix (version) | hex body | set id.
    [GeneratedRegex(@"^(..)(.*?)(TFTSet\d+(?:_Act\d+)?)$", RegexOptions.IgnoreCase)]
    private static partial Regex TeamCodeRegex();

    public async Task<RequestResult<DecodeTeamCodeResponse>> ExecuteAsync(
        DecodeTeamCodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = request.Code?.Trim() ?? string.Empty;
        var match = TeamCodeRegex().Match(code);
        if (!match.Success)
        {
            return RequestResult<DecodeTeamCodeResponse>.Fail(ApiErrors.Validation(
                "validation.invalid_team_code",
                "Invalid team code. Expected a TFT Team Planner code ending in e.g. 'TFTSet17'."));
        }

        var version = match.Groups[1].Value;
        var hex = match.Groups[2].Value;
        var setId = match.Groups[3].Value;

        // v2 ("02") packs each champion into a 3-hex (12-bit) chunk; earlier codes use 2.
        var chunkSize = string.Equals(version, "02", StringComparison.Ordinal) ? 3 : 2;
        var codes = ChunkCodes(hex, chunkSize);

        var champions = await teamPlanner.GetChampionsAsync(setId, cancellationToken);

        var units = new List<DecodedUnit>(codes.Count);
        foreach (var plannerCode in codes)
        {
            if (champions.TryGetValue(plannerCode, out var champion))
            {
                units.Add(new DecodedUnit { Name = champion.Name, Cost = champion.Cost });
            }
        }

        logger.LogInformation(
            "Decoded team code for {Set}: {Found}/{Total} champions resolved",
            setId, units.Count, codes.Count);

        return RequestResult<DecodeTeamCodeResponse>.Ok(
            new DecodeTeamCodeResponse { Set = setId, Units = units });
    }

    // Split the hex body into fixed-width chunks, parse each as a code, drop empty (0) slots.
    private static List<int> ChunkCodes(string hex, int chunkSize)
    {
        var codes = new List<int>();
        for (var i = 0; i + chunkSize <= hex.Length; i += chunkSize)
        {
            var slice = hex.Substring(i, chunkSize);
            if (int.TryParse(slice, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
                && value != 0)
            {
                codes.Add(value);
            }
        }

        return codes;
    }
}

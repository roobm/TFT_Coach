namespace TFTCoach.Seeder;

/// <summary>Minimal <c>--key value</c> / <c>--flag</c> command-line parser.</summary>
internal static class CliArgs
{
    public static Dictionary<string, string> Parse(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = args[i][2..];
            var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
            result[key] = hasValue ? args[++i] : "true";
        }

        return result;
    }
}

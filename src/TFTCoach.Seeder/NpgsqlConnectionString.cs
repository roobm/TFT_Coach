namespace TFTCoach.Seeder;

/// <summary>Converts a <c>postgresql://</c> URL into an Npgsql key/value connection string.</summary>
internal static class NpgsqlConnectionString
{
    public static string FromUrl(string value)
    {
        if (!value.StartsWith("postgres", StringComparison.OrdinalIgnoreCase) || !value.Contains("://"))
        {
            // Already a key/value connection string.
            return value;
        }

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var database = uri.AbsolutePath.Trim('/');
        var port = uri.Port > 0 ? uri.Port : 5432;

        return $"Host={uri.Host};Port={port};Username={username};Password={password};Database={database}";
    }
}

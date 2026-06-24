using System.Text.Json;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using TFTCoach.Domain.Entities;
using TFTCoach.Domain.Interfaces;

namespace TFTCoach.Infrastructure.Persistence;

/// <summary>
/// Caches computed meta snapshots in the Supabase <c>meta_cache</c> table, one row
/// per set. Saves upsert by set so a new computation replaces the previous one for
/// the same set; reads return the most recently computed set. Lets the app serve
/// the last known live meta instantly after a restart instead of recomputing from
/// the Riot ladder every cold start.
/// </summary>
public sealed class SupabaseMetaCacheRepository(
    string connectionString,
    ILogger<SupabaseMetaCacheRepository> logger) : IMetaCacheRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SaveAsync(MetaSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.Set))
        {
            return;
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                INSERT INTO meta_cache (set_key, patch, comps, computed_at)
                VALUES (@set_key, @patch, @comps, now())
                ON CONFLICT (set_key) DO UPDATE
                SET patch = excluded.patch,
                    comps = excluded.comps,
                    computed_at = excluded.computed_at
                """;

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("set_key", snapshot.Set);
            command.Parameters.AddWithValue("patch", snapshot.Patch);
            command.Parameters.Add(new NpgsqlParameter("comps", NpgsqlDbType.Jsonb)
            {
                Value = JsonSerializer.Serialize(snapshot.Comps, JsonOptions),
            });

            await command.ExecuteNonQueryAsync(cancellationToken);
            logger.LogInformation(
                "Cached meta for {Set} patch {Patch} ({Count} comps).",
                snapshot.Set, snapshot.Patch, snapshot.Comps.Count);
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "Failed to cache meta snapshot into Supabase.");
            throw;
        }
    }

    public async Task<CachedMeta?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                SELECT set_key, patch, comps, computed_at
                FROM meta_cache
                ORDER BY computed_at DESC
                LIMIT 1
                """;

            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var set = reader.GetString(0);
            var patch = reader.GetString(1);
            var compsJson = reader.GetString(2);
            var computedAt = new DateTimeOffset(reader.GetDateTime(3), TimeSpan.Zero);

            var comps = JsonSerializer.Deserialize<List<MetaComp>>(compsJson, JsonOptions) ?? [];

            return new CachedMeta
            {
                Snapshot = new MetaSnapshot { Set = set, Patch = patch, Comps = comps },
                ComputedAt = computedAt,
            };
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "Failed to read cached meta from Supabase.");
            throw;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Cached meta JSON was malformed; ignoring.");
            return null;
        }
    }
}

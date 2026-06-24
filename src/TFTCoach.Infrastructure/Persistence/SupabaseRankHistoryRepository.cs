using Microsoft.Extensions.Logging;
using Npgsql;
using TFTCoach.Domain.Entities;
using TFTCoach.Domain.Interfaces;

namespace TFTCoach.Infrastructure.Persistence;

/// <summary>
/// Persists point-in-time ranked snapshots into the Supabase <c>rank_history</c>
/// table. Skips writing a snapshot when it matches the latest stored one for the
/// same queue, so repeated fetches build a clean, de-duplicated climb history.
/// </summary>
public sealed class SupabaseRankHistoryRepository(
    string connectionString,
    ILogger<SupabaseRankHistoryRepository> logger) : IRankHistoryRepository
{
    public async Task RecordAsync(
        string puuid,
        IReadOnlyList<RankedEntry> entries,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(puuid) || entries.Count == 0)
        {
            return;
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            const string latestSql = """
                SELECT tier, division, league_points, wins, losses
                FROM rank_history
                WHERE puuid = @puuid AND queue_type = @queue_type
                ORDER BY recorded_at DESC
                LIMIT 1
                """;

            const string insertSql = """
                INSERT INTO rank_history
                    (puuid, queue_type, tier, division, league_points, wins, losses)
                VALUES
                    (@puuid, @queue_type, @tier, @division, @league_points, @wins, @losses)
                """;

            var written = 0;
            foreach (var entry in entries)
            {
                if (await MatchesLatestAsync(connection, latestSql, puuid, entry, cancellationToken))
                {
                    continue;
                }

                await using var command = new NpgsqlCommand(insertSql, connection);
                command.Parameters.AddWithValue("puuid", puuid);
                command.Parameters.AddWithValue("queue_type", entry.QueueType);
                command.Parameters.AddWithValue("tier", (object?)entry.Tier ?? DBNull.Value);
                command.Parameters.AddWithValue("division", (object?)entry.Division ?? DBNull.Value);
                command.Parameters.AddWithValue("league_points", entry.LeaguePoints);
                command.Parameters.AddWithValue("wins", entry.Wins);
                command.Parameters.AddWithValue("losses", entry.Losses);

                written += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            if (written > 0)
            {
                logger.LogInformation("Recorded {Written} new rank snapshot(s) for puuid.", written);
            }
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "Failed to record rank history into Supabase.");
            throw;
        }
    }

    public async Task<IReadOnlyList<RankSnapshot>> GetHistoryAsync(
        string puuid,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(puuid))
        {
            return [];
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                SELECT queue_type, tier, division, league_points, wins, losses, recorded_at
                FROM rank_history
                WHERE puuid = @puuid
                ORDER BY recorded_at ASC
                """;

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("puuid", puuid);

            var results = new List<RankSnapshot>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new RankSnapshot
                {
                    QueueType = reader.GetString(0),
                    Tier = reader.IsDBNull(1) ? null : reader.GetString(1),
                    Division = reader.IsDBNull(2) ? null : reader.GetString(2),
                    LeaguePoints = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                    Wins = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    Losses = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    RecordedAt = new DateTimeOffset(reader.GetDateTime(6), TimeSpan.Zero),
                });
            }

            return results;
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "Failed to read rank history from Supabase.");
            throw;
        }
    }

    private static async Task<bool> MatchesLatestAsync(
        NpgsqlConnection connection,
        string latestSql,
        string puuid,
        RankedEntry entry,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(latestSql, connection);
        command.Parameters.AddWithValue("puuid", puuid);
        command.Parameters.AddWithValue("queue_type", entry.QueueType);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return false;
        }

        var tier = reader.IsDBNull(0) ? null : reader.GetString(0);
        var division = reader.IsDBNull(1) ? null : reader.GetString(1);
        var leaguePoints = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
        var wins = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
        var losses = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);

        return tier == entry.Tier
            && division == entry.Division
            && leaguePoints == entry.LeaguePoints
            && wins == entry.Wins
            && losses == entry.Losses;
    }
}

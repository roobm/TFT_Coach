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
    private const int RankedTftQueueId = 1100;
    private const int TierBand = 400;
    private static readonly Dictionary<string, int> TierOrder = new(StringComparer.Ordinal)
    {
        ["IRON"] = 0,
        ["BRONZE"] = 1,
        ["SILVER"] = 2,
        ["GOLD"] = 3,
        ["PLATINUM"] = 4,
        ["EMERALD"] = 5,
        ["DIAMOND"] = 6,
        ["MASTER"] = 7,
        ["GRANDMASTER"] = 8,
        ["CHALLENGER"] = 9,
    };
    private static readonly Dictionary<string, int> DivisionOrder = new(StringComparer.Ordinal)
    {
        ["IV"] = 0,
        ["III"] = 1,
        ["II"] = 2,
        ["I"] = 3,
    };

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

    public async Task<IReadOnlyList<SeasonBestRank>> GetBestBySeasonAsync(
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
                SELECT
                    COALESCE(
                        (
                            SELECT mh.set_number
                            FROM match_history mh
                            WHERE mh.puuid = rh.puuid
                              AND mh.queue_id = @queue_id
                              AND mh.set_number > 0
                              AND mh.played_at <= rh.recorded_at
                            ORDER BY mh.played_at DESC
                            LIMIT 1
                        ),
                        (
                            SELECT mh2.set_number
                            FROM match_history mh2
                            WHERE mh2.puuid = rh.puuid
                              AND mh2.queue_id = @queue_id
                              AND mh2.set_number > 0
                            ORDER BY ABS(EXTRACT(EPOCH FROM (mh2.played_at - rh.recorded_at))) ASC
                            LIMIT 1
                        )
                    ) AS set_number,
                    rh.tier,
                    rh.division,
                    rh.league_points,
                    rh.recorded_at
                FROM rank_history rh
                WHERE rh.puuid = @puuid
                  AND rh.queue_type = 'RANKED_TFT'
                  AND rh.tier IS NOT NULL
                ORDER BY rh.recorded_at ASC
                """;

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("puuid", puuid);
            command.Parameters.AddWithValue("queue_id", RankedTftQueueId);

            var bestBySet = new Dictionary<int, (SeasonBestRank Rank, int Score)>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.IsDBNull(0) || reader.IsDBNull(1))
                {
                    continue;
                }

                var setNumber = reader.GetInt32(0);
                var tier = reader.GetString(1);
                var division = reader.IsDBNull(2) ? null : reader.GetString(2);
                var leaguePoints = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                var recordedAt = new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero);

                var score = RankScore(tier, division, leaguePoints);
                if (score < 0)
                {
                    continue;
                }

                var candidate = new SeasonBestRank
                {
                    SetNumber = setNumber,
                    Tier = tier,
                    Division = division,
                    LeaguePoints = leaguePoints,
                    RecordedAt = recordedAt,
                };

                if (!bestBySet.TryGetValue(setNumber, out var current)
                    || score > current.Score
                    || (score == current.Score && candidate.RecordedAt > current.Rank.RecordedAt))
                {
                    bestBySet[setNumber] = (candidate, score);
                }
            }

            return bestBySet.Values
                .Select(value => value.Rank)
                .OrderByDescending(rank => rank.SetNumber)
                .ToList();
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "Failed to read season best ranks from Supabase.");
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

    private static int RankScore(string tier, string? division, int leaguePoints)
    {
        if (!TierOrder.TryGetValue(tier, out var tierIndex))
        {
            return -1;
        }

        var baseScore = tierIndex * TierBand;
        if (tierIndex >= TierOrder["MASTER"])
        {
            return baseScore + Math.Max(0, leaguePoints);
        }

        var divisionIndex = division is not null && DivisionOrder.TryGetValue(division, out var resolvedDivision)
            ? resolvedDivision
            : 0;

        return baseScore + divisionIndex * 100 + Math.Max(0, leaguePoints);
    }
}

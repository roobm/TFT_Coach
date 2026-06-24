using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using System.Text.Json;
using TFTCoach.Domain.Entities;
using TFTCoach.Domain.Interfaces;

namespace TFTCoach.Infrastructure.Persistence;

/// <summary>
/// Persists a player's TFT matches into the Supabase <c>match_history</c> table using
/// Npgsql. Idempotent per <c>(puuid, match_id)</c> so re-analysing the same games does
/// not create duplicates.
/// </summary>
public sealed class SupabaseMatchHistoryRepository(
    string connectionString,
    ILogger<SupabaseMatchHistoryRepository> logger) : IMatchHistoryRepository
{
    public async Task SaveAsync(
        string puuid,
        IReadOnlyList<TftMatchResult> matches,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(puuid) || matches.Count == 0)
        {
            return;
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = """
                INSERT INTO match_history
                    (puuid, match_id, played_at, patch, placement, comp, augments, level,
                     traits, units, duration_seconds, players_eliminated,
                     gold_left, last_round, total_damage_to_players)
                VALUES
                    (@puuid, @match_id, @played_at, @patch, @placement, @comp, @augments, @level,
                     @traits, @units, @duration_seconds, @players_eliminated,
                     @gold_left, @last_round, @total_damage_to_players)
                ON CONFLICT (puuid, match_id) DO NOTHING
                """;

            var written = 0;
            foreach (var match in matches)
            {
                await using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("puuid", puuid);
                command.Parameters.AddWithValue("match_id", match.MatchId);
                command.Parameters.AddWithValue("played_at", match.PlayedAt.UtcDateTime);
                command.Parameters.AddWithValue("patch", (object?)match.GameVersion ?? DBNull.Value);
                command.Parameters.AddWithValue("placement", match.Placement);
                command.Parameters.AddWithValue("comp", CompLabel(match));
                command.Parameters.AddWithValue("augments", match.Augments.ToArray());
                command.Parameters.AddWithValue("level", match.Level);
                command.Parameters.AddWithValue("traits", match.Traits.ToArray());
                command.Parameters.Add(new NpgsqlParameter("units", NpgsqlDbType.Jsonb)
                {
                    Value = JsonSerializer.Serialize(match.Units, UnitsJson),
                });
                command.Parameters.AddWithValue("duration_seconds", (int)match.DurationSeconds);
                command.Parameters.AddWithValue("players_eliminated", match.PlayersEliminated);
                command.Parameters.AddWithValue("gold_left", match.GoldLeft);
                command.Parameters.AddWithValue("last_round", match.LastRound);
                command.Parameters.AddWithValue("total_damage_to_players", match.TotalDamageToPlayers);

                written += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            logger.LogInformation("Persisted {Written} new matches for puuid (of {Total}).", written, matches.Count);
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "Failed to persist match history into Supabase.");
            throw;
        }
    }

    public async Task<IReadOnlyList<TftMatchResult>> GetByPuuidAsync(
        string puuid,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(puuid) || limit <= 0)
        {
            return [];
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = $"""
                SELECT {SelectColumns}
                FROM match_history
                WHERE puuid = @puuid
                ORDER BY played_at DESC
                LIMIT @limit
                """;

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("puuid", puuid);
            command.Parameters.AddWithValue("limit", limit);

            var results = new List<TftMatchResult>(limit);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(MapRow(reader));
            }

            return results;
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "Failed to read match history from Supabase.");
            throw;
        }
    }

    public async Task<IReadOnlyList<TftMatchResult>> GetByMatchIdsAsync(
        string puuid,
        IReadOnlyList<string> matchIds,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(puuid) || matchIds.Count == 0)
        {
            return [];
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            const string sql = $"""
                SELECT {SelectColumns}
                FROM match_history
                WHERE puuid = @puuid AND match_id = ANY(@match_ids)
                """;

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("puuid", puuid);
            command.Parameters.AddWithValue("match_ids", matchIds.ToArray());

            var results = new List<TftMatchResult>(matchIds.Count);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(MapRow(reader));
            }

            return results;
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "Failed to read cached matches from Supabase.");
            throw;
        }
    }

    private const string SelectColumns =
        "match_id, played_at, patch, placement, augments, level, traits, units, " +
        "duration_seconds, players_eliminated, gold_left, last_round, total_damage_to_players";

    private static readonly JsonSerializerOptions UnitsJson = new(JsonSerializerDefaults.Web);

    private static TftMatchResult MapRow(NpgsqlDataReader reader) => new()
    {
        MatchId = reader.GetString(0),
        PlayedAt = new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero),
        GameVersion = reader.IsDBNull(2) ? null : reader.GetString(2),
        Placement = reader.GetInt32(3),
        Augments = reader.IsDBNull(4) ? [] : reader.GetFieldValue<string[]>(4),
        Level = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
        Traits = reader.IsDBNull(6) ? [] : reader.GetFieldValue<string[]>(6),
        Units = reader.IsDBNull(7)
            ? []
            : JsonSerializer.Deserialize<List<UnitDetail>>(reader.GetFieldValue<string>(7), UnitsJson) ?? [],
        DurationSeconds = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
        PlayersEliminated = reader.IsDBNull(9) ? 0 : reader.GetInt32(9),
        GoldLeft = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
        LastRound = reader.IsDBNull(11) ? 0 : reader.GetInt32(11),
        TotalDamageToPlayers = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
    };

    // A comp is identified by its leading one or two traits (already sorted highest-first).
    private static string CompLabel(TftMatchResult match)
    {
        var traitNames = new List<string>(2);
        foreach (var trait in match.Traits)
        {
            var lastSpace = trait.LastIndexOf(' ');
            var name = (lastSpace > 0 ? trait[..lastSpace] : trait).Trim();
            if (name.Length > 0)
            {
                traitNames.Add(name);
            }

            if (traitNames.Count == 2)
            {
                break;
            }
        }

        var label = traitNames.Count == 0 ? "Unknown" : string.Join(" + ", traitNames);
        return label.Length > 100 ? label[..100] : label;
    }
}

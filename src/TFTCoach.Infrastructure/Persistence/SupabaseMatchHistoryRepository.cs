using Microsoft.Extensions.Logging;
using Npgsql;
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
                    (puuid, match_id, played_at, patch, placement, comp, augments, level)
                VALUES
                    (@puuid, @match_id, @played_at, @patch, @placement, @comp, @augments, @level)
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

            const string sql = """
                SELECT match_id, played_at, patch, placement, comp, augments, level
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
                results.Add(new TftMatchResult
                {
                    MatchId = reader.GetString(0),
                    PlayedAt = new DateTimeOffset(reader.GetDateTime(1), TimeSpan.Zero),
                    GameVersion = reader.IsDBNull(2) ? null : reader.GetString(2),
                    Placement = reader.GetInt32(3),
                    Augments = reader.IsDBNull(5) ? [] : reader.GetFieldValue<string[]>(5),
                    Level = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                });
            }

            return results;
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "Failed to read match history from Supabase.");
            throw;
        }
    }

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

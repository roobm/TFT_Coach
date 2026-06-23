using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Npgsql;
using TFTCoach.Domain.Entities;
using TFTCoach.Domain.Interfaces;

namespace TFTCoach.Infrastructure.Persistence;

/// <summary>
/// Persists embedded patch documents into the Supabase <c>tft_patch_data</c> table
/// using Npgsql and pgvector.
/// </summary>
public sealed class SupabaseVectorRepository(
    string connectionString,
    ILogger<SupabaseVectorRepository> logger) : IVectorRepository
{
    public async Task<int> UpsertAsync(IReadOnlyList<PatchDocument> documents, CancellationToken cancellationToken = default)
    {
        if (documents.Count == 0)
        {
            return 0;
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var written = 0;
            foreach (var document in documents)
            {
                const string sql = """
                    INSERT INTO tft_patch_data (patch, category, content, embedding)
                    VALUES (@patch, @category, @content, @embedding::vector)
                    """;

                await using var command = new NpgsqlCommand(sql, connection);
                command.Parameters.AddWithValue("patch", document.Patch);
                command.Parameters.AddWithValue("category", document.Category);
                command.Parameters.AddWithValue("content", document.Content);
                command.Parameters.AddWithValue("embedding", ToVectorLiteral(document.Embedding));

                written += await command.ExecuteNonQueryAsync(cancellationToken);
            }

            logger.LogInformation("Upserted {Count} documents into tft_patch_data", written);
            return written;
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "Failed to upsert patch documents into Supabase.");
            throw;
        }
    }

    public async Task<IReadOnlyList<string>> SearchAsync(
        IReadOnlyList<float> embedding,
        int topK,
        CancellationToken cancellationToken = default)
    {
        if (embedding.Count == 0 || topK <= 0)
        {
            return [];
        }

        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Cosine distance (<=>) ascending returns the most similar documents first.
            const string sql = """
                SELECT content
                FROM tft_patch_data
                WHERE content IS NOT NULL
                ORDER BY embedding <=> @query::vector
                LIMIT @limit
                """;

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("query", ToVectorLiteral(embedding));
            command.Parameters.AddWithValue("limit", topK);

            var results = new List<string>(topK);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    results.Add(reader.GetString(0));
                }
            }

            logger.LogInformation("Vector search returned {Count} documents from tft_patch_data", results.Count);
            return results;
        }
        catch (NpgsqlException ex)
        {
            logger.LogError(ex, "Failed to search patch documents in Supabase.");
            throw;
        }
    }

    /// <summary>Formats a vector as the pgvector text literal <c>[v1,v2,...]</c>.</summary>
    private static string ToVectorLiteral(IReadOnlyList<float> embedding)
    {
        var builder = new StringBuilder(embedding.Count * 8 + 2);
        builder.Append('[');
        for (var i = 0; i < embedding.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(embedding[i].ToString("R", CultureInfo.InvariantCulture));
        }

        builder.Append(']');
        return builder.ToString();
    }
}

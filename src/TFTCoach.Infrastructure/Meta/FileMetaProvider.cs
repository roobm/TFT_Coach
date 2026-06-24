using System.Text.Json;
using Microsoft.Extensions.Logging;
using TFTCoach.Application.Interfaces;
using TFTCoach.Domain.Entities;

namespace TFTCoach.Infrastructure.Meta;

/// <summary>
/// Reads the current TFT meta from a curated, patch-tagged JSON file on disk and
/// caches it after the first successful read. A missing or malformed file is not
/// fatal — it logs and returns <c>null</c> so live coaching falls back to
/// first-principles advice. Designed to be swapped for a community-data-backed
/// provider behind <see cref="IMetaProvider"/> without touching callers.
/// </summary>
public sealed class FileMetaProvider(string filePath, ILogger<FileMetaProvider> logger) : IMetaProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim gate = new(1, 1);
    private MetaSnapshot? cached;

    public async Task<MetaSnapshot?> GetCurrentMetaAsync(CancellationToken cancellationToken = default)
    {
        if (cached is not null)
        {
            return cached;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            if (cached is not null)
            {
                return cached;
            }

            if (!File.Exists(filePath))
            {
                logger.LogWarning(
                    "Meta file not found at {Path}; live coaching will run without meta grounding.", filePath);
                return null;
            }

            await using var stream = File.OpenRead(filePath);
            var dto = await JsonSerializer.DeserializeAsync<MetaFileDto>(stream, JsonOptions, cancellationToken);

            if (dto is null || string.IsNullOrWhiteSpace(dto.Set) || string.IsNullOrWhiteSpace(dto.Patch))
            {
                logger.LogWarning("Meta file at {Path} was empty or missing set/patch.", filePath);
                return null;
            }

            cached = new MetaSnapshot
            {
                Set = dto.Set,
                Patch = dto.Patch,
                Comps = (dto.Comps ?? [])
                    .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c => new MetaComp
                    {
                        Name = c.Name!,
                        Tier = c.Tier ?? "",
                        Playstyle = c.Playstyle ?? "",
                        Carries = c.Carries ?? [],
                        KeyItems = c.KeyItems ?? [],
                        Notes = c.Notes,
                    })
                    .ToList(),
            };

            logger.LogInformation(
                "Loaded TFT meta {Set} patch {Patch} with {Count} comps.", cached.Set, cached.Patch, cached.Comps.Count);
            return cached;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogError(ex, "Failed to read meta file at {Path}.", filePath);
            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    private sealed class MetaFileDto
    {
        public string? Set { get; set; }
        public string? Patch { get; set; }
        public List<MetaCompDto>? Comps { get; set; }
    }

    private sealed class MetaCompDto
    {
        public string? Name { get; set; }
        public string? Tier { get; set; }
        public string? Playstyle { get; set; }
        public List<string>? Carries { get; set; }
        public List<string>? KeyItems { get; set; }
        public string? Notes { get; set; }
    }
}

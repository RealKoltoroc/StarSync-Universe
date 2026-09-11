using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StarSyncUniverse.Services;

public sealed record LocationUserOverrideRecord(
    string Key,
    string Name,
    string? LocationUuid,
    string? PlacementId,
    string? SurfaceAnchorId,
    string? Description,
    string? ImageFileName,
    DateTimeOffset UpdatedUtc)
{
    public string? BrowserImageUrl => string.IsNullOrWhiteSpace(ImageFileName)
        ? null
        : "https://starsync-location-overrides/" + Uri.EscapeDataString(ImageFileName);
}

/// <summary>
/// Local, user-authored presentation metadata for universe locations/objects.
/// Local image/description overrides are intentionally independent of Data.p4k,
/// SCUnpacked and online enrichment. They always win while present and deleting
/// the local value restores the normal external/bundled fallback chain.
/// </summary>
public sealed class LocationUserOverrideService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public string RootPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StarSyncUniverse", "LocationOverrides");

    public string ImageRootPath => Path.Combine(RootPath, "Images");
    public string MetadataPath => Path.Combine(RootPath, "location-overrides.json");

    public async Task<IReadOnlyList<LocationUserOverrideRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return await LoadUnlockedAsync(cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    public async Task<LocationUserOverrideRecord?> SetDescriptionAsync(
        string key,
        string name,
        string? locationUuid,
        string? placementId,
        string? surfaceAnchorId,
        string description,
        CancellationToken cancellationToken = default)
    {
        key = NormalizeKey(key);
        description = (description ?? string.Empty).Trim();
        if (description.Length == 0)
            return await DeleteDescriptionAsync(key, cancellationToken).ConfigureAwait(false);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var records = (await LoadUnlockedAsync(cancellationToken).ConfigureAwait(false)).ToList();
            var index = records.FindIndex(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            var existing = index >= 0 ? records[index] : null;
            var updated = new LocationUserOverrideRecord(
                key,
                FirstNonEmpty(name, existing?.Name, key),
                FirstNonEmptyNullable(locationUuid, existing?.LocationUuid),
                FirstNonEmptyNullable(placementId, existing?.PlacementId),
                FirstNonEmptyNullable(surfaceAnchorId, existing?.SurfaceAnchorId),
                description,
                existing?.ImageFileName,
                DateTimeOffset.UtcNow);
            if (index >= 0) records[index] = updated; else records.Add(updated);
            await SaveUnlockedAsync(records, cancellationToken).ConfigureAwait(false);
            return updated;
        }
        finally { _gate.Release(); }
    }

    public async Task<LocationUserOverrideRecord?> DeleteDescriptionAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        key = NormalizeKey(key);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var records = (await LoadUnlockedAsync(cancellationToken).ConfigureAwait(false)).ToList();
            var index = records.FindIndex(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return null;
            var current = records[index];
            if (string.IsNullOrWhiteSpace(current.ImageFileName))
            {
                records.RemoveAt(index);
                await SaveUnlockedAsync(records, cancellationToken).ConfigureAwait(false);
                return null;
            }
            var updated = current with { Description = null, UpdatedUtc = DateTimeOffset.UtcNow };
            records[index] = updated;
            await SaveUnlockedAsync(records, cancellationToken).ConfigureAwait(false);
            return updated;
        }
        finally { _gate.Release(); }
    }

    public async Task<LocationUserOverrideRecord> SetImageAsync(
        string key,
        string name,
        string? locationUuid,
        string? placementId,
        string? surfaceAnchorId,
        string sourcePath,
        CancellationToken cancellationToken = default)
    {
        key = NormalizeKey(key);
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new FileNotFoundException("Selected image file was not found.", sourcePath);

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension is not ".png" and not ".jpg" and not ".jpeg" and not ".webp")
            throw new InvalidDataException("Supported image formats are PNG, JPEG and WebP.");
        var info = new FileInfo(sourcePath);
        if (info.Length <= 0 || info.Length > 24L * 1024L * 1024L)
            throw new InvalidDataException("Image file must be between 1 byte and 24 MiB.");

        Directory.CreateDirectory(ImageRootPath);
        var fileName = BuildImageFileName(key, extension == ".jpeg" ? ".jpg" : extension);
        var destination = Path.Combine(ImageRootPath, fileName);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var records = (await LoadUnlockedAsync(cancellationToken).ConfigureAwait(false)).ToList();
            var index = records.FindIndex(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            var existing = index >= 0 ? records[index] : null;

            var temp = destination + ".tmp-" + Guid.NewGuid().ToString("N");
            await using (var input = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            File.Move(temp, destination, overwrite: true);

            if (!string.IsNullOrWhiteSpace(existing?.ImageFileName) &&
                !string.Equals(existing.ImageFileName, fileName, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteImage(existing.ImageFileName);
            }

            var updated = new LocationUserOverrideRecord(
                key,
                FirstNonEmpty(name, existing?.Name, key),
                FirstNonEmptyNullable(locationUuid, existing?.LocationUuid),
                FirstNonEmptyNullable(placementId, existing?.PlacementId),
                FirstNonEmptyNullable(surfaceAnchorId, existing?.SurfaceAnchorId),
                existing?.Description,
                fileName,
                DateTimeOffset.UtcNow);
            if (index >= 0) records[index] = updated; else records.Add(updated);
            await SaveUnlockedAsync(records, cancellationToken).ConfigureAwait(false);
            return updated;
        }
        finally { _gate.Release(); }
    }

    public async Task<LocationUserOverrideRecord?> DeleteImageAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        key = NormalizeKey(key);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var records = (await LoadUnlockedAsync(cancellationToken).ConfigureAwait(false)).ToList();
            var index = records.FindIndex(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return null;
            var current = records[index];
            if (!string.IsNullOrWhiteSpace(current.ImageFileName)) TryDeleteImage(current.ImageFileName);
            if (string.IsNullOrWhiteSpace(current.Description))
            {
                records.RemoveAt(index);
                await SaveUnlockedAsync(records, cancellationToken).ConfigureAwait(false);
                return null;
            }
            var updated = current with { ImageFileName = null, UpdatedUtc = DateTimeOffset.UtcNow };
            records[index] = updated;
            await SaveUnlockedAsync(records, cancellationToken).ConfigureAwait(false);
            return updated;
        }
        finally { _gate.Release(); }
    }

    private async Task<IReadOnlyList<LocationUserOverrideRecord>> LoadUnlockedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(MetadataPath)) return [];
        try
        {
            await using var stream = new FileStream(MetadataPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var result = await JsonSerializer.DeserializeAsync<List<LocationUserOverrideRecord>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            return result?.Where(x => !string.IsNullOrWhiteSpace(x.Key)).ToArray() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task SaveUnlockedAsync(IReadOnlyList<LocationUserOverrideRecord> records, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(RootPath);
        var temp = MetadataPath + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            await JsonSerializer.SerializeAsync(stream, records.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase), JsonOptions, cancellationToken).ConfigureAwait(false);
        File.Move(temp, MetadataPath, overwrite: true);
    }

    private void TryDeleteImage(string fileName)
    {
        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(ImageRootPath, fileName));
            var root = Path.GetFullPath(ImageRootPath) + Path.DirectorySeparatorChar;
            if (fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath)) File.Delete(fullPath);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string BuildImageFileName(string key, string extension)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key.ToLowerInvariant()));
        return Convert.ToHexString(hash).ToLowerInvariant()[..32] + extension;
    }

    private static string NormalizeKey(string value)
    {
        var key = (value ?? string.Empty).Trim();
        if (key.Length == 0) throw new ArgumentException("Location override key is required.", nameof(value));
        return key;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? string.Empty;

    private static string? FirstNonEmptyNullable(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
}

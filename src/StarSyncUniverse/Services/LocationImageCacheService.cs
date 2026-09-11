using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StarSyncUniverse.Services;

public sealed record LocationImageAsset(
    string LocationUuid,
    string LocationName,
    bool Exists,
    string? LocalPath,
    string? BrowserAssetUrl,
    string? SourceUrl,
    string Provider,
    string DataStatus,
    string? Description = null,
    IReadOnlyDictionary<string, string>? Details = null,
    string? SourceVersion = null,
    DateTimeOffset? SourceUpdatedAt = null);

internal sealed record ResolvedLocationEnrichment(
    string? ImageUrl,
    string? Description,
    IReadOnlyDictionary<string, string> Details,
    string? Version,
    DateTimeOffset? UpdatedAt,
    string Provider);

/// <summary>
/// Lazy, attribution-preserving online enrichment cache for location descriptions/details and imagery.
/// Remote data is optional presentation/metadata enrichment only. Packaged canonical identity and spatial geometry remain usable without network access.
/// </summary>
public sealed class LocationImageCacheService
{
    private static readonly HttpClient Client = CreateClient();
    private readonly SemaphoreSlim _gate = new(3, 3);

    public LocationImageCacheService()
    {
        RootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StarSyncUniverse",
            "LocationImages");
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public async Task<LocationImageAsset> GetOrDownloadAsync(
        string? locationUuid,
        string locationName,
        CancellationToken cancellationToken = default)
    {
        locationUuid ??= string.Empty;
        locationName = locationName?.Trim() ?? string.Empty;
        if (locationName.Length == 0)
            return Missing(locationUuid, locationName, "INVALID_LOCATION_NAME");

        var key = BuildKey(locationUuid, locationName);
        var metadataPath = Path.Combine(RootPath, key + ".json");
        var cached = await TryReadMetadataAsync(metadataPath, cancellationToken);
        if (cached is not null && cached.Exists && !string.IsNullOrWhiteSpace(cached.LocalPath) && File.Exists(cached.LocalPath))
            return cached with { BrowserAssetUrl = BuildBrowserUrl(Path.GetFileName(cached.LocalPath)) };
        if (cached is not null && !cached.Exists && File.Exists(metadataPath))
        {
            var age = DateTimeOffset.UtcNow - File.GetLastWriteTimeUtc(metadataPath);
            if (age < TimeSpan.FromHours(12)) return cached;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            cached = await TryReadMetadataAsync(metadataPath, cancellationToken);
            if (cached is not null && cached.Exists && !string.IsNullOrWhiteSpace(cached.LocalPath) && File.Exists(cached.LocalPath))
                return cached with { BrowserAssetUrl = BuildBrowserUrl(Path.GetFileName(cached.LocalPath)) };

            var enrichment = await ResolveEnrichmentAsync(locationUuid, locationName, cancellationToken);
            if (enrichment is null)
            {
                var missing = Missing(locationUuid, locationName, "NO_ONLINE_LOCATION_MATCH");
                await WriteMetadataAsync(metadataPath, missing, cancellationToken);
                return missing;
            }

            var candidate = enrichment.ImageUrl;
            if (string.IsNullOrWhiteSpace(candidate) || !IsAllowedRemoteUri(candidate))
            {
                var textOnly = new LocationImageAsset(
                    locationUuid,
                    locationName,
                    false,
                    null,
                    null,
                    null,
                    enrichment.Provider,
                    "ONLINE_TEXT_ENRICHMENT_CACHED_NO_IMAGE",
                    enrichment.Description,
                    enrichment.Details,
                    enrichment.Version,
                    enrichment.UpdatedAt);
                await WriteMetadataAsync(metadataPath, textOnly, cancellationToken);
                return textOnly;
            }

            using var response = await Client.GetAsync(candidate, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var missing = new LocationImageAsset(
                    locationUuid,
                    locationName,
                    false,
                    null,
                    null,
                    candidate,
                    enrichment.Provider,
                    $"IMAGE_HTTP_{(int)response.StatusCode}_TEXT_ENRICHMENT_PRESERVED",
                    enrichment.Description,
                    enrichment.Details,
                    enrichment.Version,
                    enrichment.UpdatedAt);
                await WriteMetadataAsync(metadataPath, missing, cancellationToken);
                return missing;
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                var missing = new LocationImageAsset(
                    locationUuid,
                    locationName,
                    false,
                    null,
                    null,
                    candidate,
                    enrichment.Provider,
                    "NON_IMAGE_RESPONSE_TEXT_ENRICHMENT_PRESERVED",
                    enrichment.Description,
                    enrichment.Details,
                    enrichment.Version,
                    enrichment.UpdatedAt);
                await WriteMetadataAsync(metadataPath, missing, cancellationToken);
                return missing;
            }

            var extension = ExtensionFrom(mediaType, candidate);
            var localPath = Path.Combine(RootPath, key + extension);
            var tempPath = localPath + ".tmp";
            try
            {
                await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
                await using (var output = File.Create(tempPath))
                {
                    var buffer = new byte[64 * 1024];
                    long total = 0;
                    while (true)
                    {
                        var read = await input.ReadAsync(buffer, cancellationToken);
                        if (read <= 0) break;
                        total += read;
                        if (total > 16 * 1024 * 1024)
                            throw new InvalidDataException("Location image exceeded the 16 MiB cache limit.");
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    }
                }
                File.Move(tempPath, localPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); }
                    catch { }
                }
            }

            var asset = new LocationImageAsset(
                locationUuid,
                locationName,
                true,
                localPath,
                BuildBrowserUrl(Path.GetFileName(localPath)),
                candidate,
                enrichment.Provider,
                "ONLINE_LOCATION_ENRICHMENT_CACHED",
                enrichment.Description,
                enrichment.Details,
                enrichment.Version,
                enrichment.UpdatedAt);
            await WriteMetadataAsync(metadataPath, asset, cancellationToken);
            return asset;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            var missing = Missing(locationUuid, locationName, "IMAGE_CACHE_ERROR: " + ex.Message);
            await WriteMetadataAsync(metadataPath, missing, CancellationToken.None);
            return missing;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static HttpClient CreateClient()
    {
        // Redirects are disabled deliberately: a trusted API/image host must not be able to redirect
        // the client into localhost/private-network resources (SSRF). Returned URLs are validated
        // by IsAllowedRemoteUri before any image request is sent.
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        var client = new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromSeconds(18) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StarSyncUniverse", "0.7"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static bool IsAllowedRemoteUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        if (!uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;
        var host = uri.IdnHost;
        return host.Equals("api.star-citizen.wiki", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".star-citizen.wiki", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("starcitizen.tools", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".starcitizen.tools", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ResolvedLocationEnrichment?> ResolveEnrichmentAsync(
        string locationUuid,
        string locationName,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(locationUuid))
        {
            var direct = $"https://api.star-citizen.wiki/api/locations/{Uri.EscapeDataString(locationUuid)}";
            var resolved = await QueryEnrichmentAsync(direct, locationName, cancellationToken);
            if (resolved is not null) return resolved;
        }

        var query = $"https://api.star-citizen.wiki/api/locations?filter%5Bquery%5D={Uri.EscapeDataString(locationName)}";
        return await QueryEnrichmentAsync(query, locationName, cancellationToken);
    }

    private static async Task<ResolvedLocationEnrichment?> QueryEnrichmentAsync(
        string url,
        string locationName,
        CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var objects = EnumerateObjects(json.RootElement).ToArray();
        var selected = objects.FirstOrDefault(x =>
            string.Equals(GetString(x, "name"), locationName, StringComparison.OrdinalIgnoreCase));
        if (selected.ValueKind != JsonValueKind.Object)
        {
            selected = objects.FirstOrDefault(x =>
                x.ValueKind == JsonValueKind.Object && !string.IsNullOrWhiteSpace(GetString(x, "name")));
        }
        if (selected.ValueKind != JsonValueKind.Object) return null;

        string? image = FindImageUrl(selected);
        if (string.IsNullOrWhiteSpace(image))
        {
            foreach (var obj in objects)
            {
                image = FindImageUrl(obj);
                if (!string.IsNullOrWhiteSpace(image)) break;
            }
        }
        var normalizedImage = string.IsNullOrWhiteSpace(image) ? null : NormalizeAssetUrl(image);

        var details = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        static void AddDetail(IDictionary<string, string> target, string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) target[key] = value.Trim();
        }
        static string? Scalar(JsonElement e, string name)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var p)) return null;
            return p.ValueKind switch
            {
                JsonValueKind.String => p.GetString(),
                JsonValueKind.Number => p.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
        }
        static string? NestedName(JsonElement e, string name)
        {
            if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(name, out var p)) return null;
            if (p.ValueKind == JsonValueKind.String) return p.GetString();
            return p.ValueKind == JsonValueKind.Object ? GetString(p, "name") : null;
        }

        AddDetail(details, "System", NestedName(selected, "system"));
        AddDetail(details, "Parent", NestedName(selected, "parent"));
        AddDetail(details, "Type", NestedName(selected, "type"));
        AddDetail(details, "Jurisdiction", NestedName(selected, "jurisdiction"));
        AddDetail(details, "Affiliation", NestedName(selected, "affiliation"));
        AddDetail(details, "Size", Scalar(selected, "size"));
        AddDetail(details, "Respawn location type", Scalar(selected, "respawn_location_type"));
        AddDetail(details, "Child count", Scalar(selected, "child_count"));
        AddDetail(details, "Mission count", Scalar(selected, "mission_count"));
        AddDetail(details, "Scannable", Scalar(selected, "is_scannable"));
        AddDetail(details, "Hidden in starmap", Scalar(selected, "hide_in_starmap"));

        if (selected.TryGetProperty("quantum_travel", out var qt) && qt.ValueKind == JsonValueKind.Object)
        {
            AddDetail(details, "QT arrival radius", Scalar(qt, "arrival_radius_formatted") ?? Scalar(qt, "arrival_radius"));
            AddDetail(details, "QT adoption radius", Scalar(qt, "adoption_radius_formatted") ?? Scalar(qt, "adoption_radius"));
            AddDetail(details, "QT obstruction radius", Scalar(qt, "obstruction_radius_formatted") ?? Scalar(qt, "obstruction_radius"));
        }

        var version = Scalar(selected, "version");
        AddDetail(details, "Source version", version);
        DateTimeOffset? updatedAt = null;
        var updatedText = Scalar(selected, "updated_at");
        if (DateTimeOffset.TryParse(updatedText, out var parsedUpdatedAt)) updatedAt = parsedUpdatedAt;
        if (updatedAt is not null) AddDetail(details, "Source updated", updatedAt.Value.ToString("O"));

        return new ResolvedLocationEnrichment(
            normalizedImage,
            GetString(selected, "description")?.Trim(),
            details,
            version,
            updatedAt,
            normalizedImage is null ? "Star-Citizen.wiki API" : ProviderFor(normalizedImage));
    }

    private static IEnumerable<JsonElement> EnumerateObjects(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            yield return element;
            foreach (var property in element.EnumerateObject())
                foreach (var child in EnumerateObjects(property.Value))
                    yield return child;
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                foreach (var child in EnumerateObjects(item))
                    yield return child;
        }
    }

    private static string? FindImageUrl(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        string? fallback = null;
        foreach (var property in element.EnumerateObject())
        {
            var propertyName = property.Name.ToLowerInvariant();
            var imageContext = propertyName.Contains("image", StringComparison.Ordinal) ||
                               propertyName.Contains("thumbnail", StringComparison.Ordinal) ||
                               propertyName.Contains("media", StringComparison.Ordinal) ||
                               propertyName.Contains("photo", StringComparison.Ordinal) ||
                               propertyName.Contains("picture", StringComparison.Ordinal) ||
                               propertyName.Contains("gallery", StringComparison.Ordinal) ||
                               propertyName.Contains("screenshot", StringComparison.Ordinal);
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                var value = property.Value.GetString();
                if (!IsLikelyImageUrl(value, imageContext)) continue;
                if (imageContext) return value;
                fallback ??= value;
            }
            else if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                var nested = property.Value.ValueKind == JsonValueKind.Object
                    ? FindImageUrl(property.Value)
                    : property.Value.EnumerateArray().Select(FindImageUrl).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                if (!string.IsNullOrWhiteSpace(nested))
                {
                    if (imageContext) return nested;
                    fallback ??= nested;
                }
            }
        }
        return fallback;
    }

    private static bool IsLikelyImageUrl(string? value, bool imageContext)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var lower = value.Trim().ToLowerInvariant();
        if (lower.Contains("/api/", StringComparison.Ordinal)) return false;
        return lower.EndsWith(".png", StringComparison.Ordinal) ||
               lower.EndsWith(".jpg", StringComparison.Ordinal) ||
               lower.EndsWith(".jpeg", StringComparison.Ordinal) ||
               lower.EndsWith(".webp", StringComparison.Ordinal) ||
               lower.EndsWith(".avif", StringComparison.Ordinal) ||
               lower.Contains(".png?", StringComparison.Ordinal) ||
               lower.Contains(".jpg?", StringComparison.Ordinal) ||
               lower.Contains(".jpeg?", StringComparison.Ordinal) ||
               lower.Contains(".webp?", StringComparison.Ordinal) ||
               lower.Contains("media.starcitizen.tools", StringComparison.Ordinal) ||
               imageContext && (lower.Contains("star-citizen.wiki", StringComparison.Ordinal) || lower.Contains("/storage/", StringComparison.Ordinal));
    }

    private static string NormalizeAssetUrl(string value)
    {
        value = value.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri)) return uri.AbsoluteUri;
        if (value.StartsWith("/", StringComparison.Ordinal)) return "https://api.star-citizen.wiki" + value;
        return "https://api.star-citizen.wiki/" + value.TrimStart('/');
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property)) return null;
        return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
    }

    private static string ExtensionFrom(string mediaType, string url)
    {
        if (mediaType.Contains("png", StringComparison.OrdinalIgnoreCase)) return ".png";
        if (mediaType.Contains("webp", StringComparison.OrdinalIgnoreCase)) return ".webp";
        if (mediaType.Contains("avif", StringComparison.OrdinalIgnoreCase)) return ".avif";
        if (mediaType.Contains("gif", StringComparison.OrdinalIgnoreCase)) return ".gif";
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            if (ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".avif" or ".gif") return ext;
        }
        return ".jpg";
    }

    private static string BuildKey(string uuid, string name)
    {
        if (Guid.TryParse(uuid, out var guid)) return guid.ToString("N");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(name.ToLowerInvariant()));
        return Convert.ToHexString(bytes.AsSpan(0, 12)).ToLowerInvariant();
    }

    private static string BuildBrowserUrl(string fileName) =>
        "https://starsync-location-images/" + Uri.EscapeDataString(fileName);

    private static string ProviderFor(string sourceUrl)
    {
        if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) &&
            (uri.Host.Equals("media.starcitizen.tools", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.EndsWith(".starcitizen.tools", StringComparison.OrdinalIgnoreCase)))
            return "starcitizen.tools via Star-Citizen.wiki API";
        return "Star-Citizen.wiki";
    }

    private static LocationImageAsset Missing(string uuid, string name, string status) =>
        new(uuid, name, false, null, null, null, "Star-Citizen.wiki", status);

    private static async Task<LocationImageAsset?> TryReadMetadataAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return null;
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<LocationImageAsset>(stream, cancellationToken: cancellationToken);
        }
        catch { return null; }
    }

    private static async Task WriteMetadataAsync(string path, LocationImageAsset asset, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, asset, cancellationToken: cancellationToken);
    }
}

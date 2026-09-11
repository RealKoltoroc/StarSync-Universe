using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Data;

/// <summary>
/// Promotes placed CURRENT LIVE object containers whose root bounds directly describe a
/// spatial phenomenon (currently Nyx gas-cloud and Glaciem-ring segments) into SpatialVolume.
/// The importer groups by source template so each shared SOC is extracted and parsed once.
/// </summary>
public sealed class PlacedSpatialVolumeImporter
{
    private readonly StarBreakerClient _starBreaker;

    public PlacedSpatialVolumeImporter(StarBreakerClient starBreaker) => _starBreaker = starBreaker;

    public async Task<IReadOnlyList<SpatialVolume>> ImportAsync(
        UniverseDataset dataset,
        CancellationToken cancellationToken = default)
    {
        var candidates = dataset.Entities
            .Where(e => IsSupported(e.SourcePath))
            .Where(e => e.SourcePath?.EndsWith(".socpak", StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();

        if (candidates.Length == 0)
            return [];

        var boundsBySource = new Dictionary<string, TemplateBounds>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in candidates.GroupBy(e => e.SourcePath!, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourcePath = group.Key;
            var fileName = Path.GetFileName(sourcePath.Replace('/', Path.DirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            var cacheName = "spatial-volume-" + Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
            string extractRoot;
            try
            {
                // Filename-only glob avoids case-sensitive directory mismatches such as glaciemRing/glaciemring.
                extractRoot = await _starBreaker.ExtractAsync($"**/{fileName}", cacheName, cancellationToken, convert: "cryxml");
            }
            catch
            {
                continue;
            }

            var socpak = Directory.EnumerateFiles(extractRoot, fileName, SearchOption.AllDirectories).FirstOrDefault();
            if (socpak is null)
                continue;

            var bounds = TryReadBounds(socpak);
            if (bounds is not null)
                boundsBySource[sourcePath] = bounds;
        }

        var result = new List<SpatialVolume>(candidates.Length);
        foreach (var entity in candidates)
        {
            if (entity.SourcePath is null || !boundsBySource.TryGetValue(entity.SourcePath, out var bounds))
                continue;
            if (bounds.Radius <= 0 || bounds.Max.X <= bounds.Min.X || bounds.Max.Y <= bounds.Min.Y || bounds.Max.Z <= bounds.Min.Z)
                continue;

            result.Add(new SpatialVolume(
                Id: $"volume:{entity.Id}",
                Name: entity.Name,
                Type: Classify(entity.SourcePath),
                System: dataset.System,
                ParentFrameId: entity.ParentId is null ? $"system:{dataset.System}" : $"frame:{entity.ParentId}",
                PlacementId: entity.Id,
                CenterX: entity.X,
                CenterY: entity.Y,
                CenterZ: entity.Z,
                MinLocalX: bounds.Min.X,
                MinLocalY: bounds.Min.Y,
                MinLocalZ: bounds.Min.Z,
                MaxLocalX: bounds.Max.X,
                MaxLocalY: bounds.Max.Y,
                MaxLocalZ: bounds.Max.Z,
                BoundingRadiusMeters: bounds.Radius,
                TemplateSourcePath: entity.SourcePath,
                SourceAuthority: "Data.p4k/System ObjectContainer placement + CURRENT LIVE ObjectContainer root bounds",
                DataStatus: "LOCAL_DIRECT"));
        }

        dataset.Diagnostics.Add($"Placed spatial volumes: {result.Count} gas-cloud/ring-segment volume(s) resolved from {boundsBySource.Count} unique CURRENT LIVE template(s).");
        return result;
    }

    private static bool IsSupported(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)) return false;
        var p = sourcePath.Replace('\\', '/');
        return p.Contains("/gascloudsgen/", StringComparison.OrdinalIgnoreCase) ||
               p.Contains("/glaciemring/", StringComparison.OrdinalIgnoreCase);
    }

    private static string Classify(string sourcePath) =>
        sourcePath.Contains("gascloud", StringComparison.OrdinalIgnoreCase)
            ? "GasCloudVolume"
            : "RingSegmentVolume";

    private static TemplateBounds? TryReadBounds(string socpak)
    {
        try
        {
            using var zip = ZipFile.OpenRead(socpak);
            var baseName = Path.GetFileNameWithoutExtension(socpak);
            var entry = zip.Entries.FirstOrDefault(e => e.FullName.Equals(baseName + ".xml", StringComparison.OrdinalIgnoreCase))
                ?? zip.Entries.FirstOrDefault(e => e.FullName.EndsWith("/" + baseName + ".xml", StringComparison.OrdinalIgnoreCase));
            if (entry is null) return null;

            using var reader = new StreamReader(entry.Open());
            var doc = XDocument.Load(reader, LoadOptions.None);
            var root = doc.Root;
            if (root is null) return null;

            var min = ParseVector3(root.Attribute("minBounds")?.Value);
            var max = ParseVector3(root.Attribute("maxBounds")?.Value);
            var radius = Parse(root.Attribute("radius")?.Value);
            return new TemplateBounds(min, max, radius);
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    private static double Parse(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0d;

    private static (double X, double Y, double Z) ParseVector3(string? value)
    {
        var parts = (value ?? string.Empty).Split(',');
        return (
            parts.Length > 0 ? Parse(parts[0]) : 0d,
            parts.Length > 1 ? Parse(parts[1]) : 0d,
            parts.Length > 2 ? Parse(parts[2]) : 0d);
    }

    private sealed record TemplateBounds(
        (double X, double Y, double Z) Min,
        (double X, double Y, double Z) Max,
        double Radius);
}

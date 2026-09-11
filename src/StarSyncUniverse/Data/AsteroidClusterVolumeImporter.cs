using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Data;

public sealed class AsteroidClusterVolumeImporter
{
    private readonly StarBreakerClient _starBreaker;

    public AsteroidClusterVolumeImporter(StarBreakerClient starBreaker) => _starBreaker = starBreaker;

    public async Task<IReadOnlyList<SpatialVolume>> ImportAsync(
        UniverseDataset dataset,
        CancellationToken cancellationToken = default)
    {
        var candidates = dataset.Entities
            .Where(e => e.SourcePath?.Contains("asteroidcluster", StringComparison.OrdinalIgnoreCase) == true ||
                        e.SourcePath?.Contains("cluster_modular", StringComparison.OrdinalIgnoreCase) == true)
            .Where(e => e.SourcePath?.EndsWith(".socpak", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        if (candidates.Count == 0)
            return [];

        const string filter = "Data/ObjectContainers/PU/asteroidCluster/clusters_modular/clusters_mod_set/cluster_modular_warm_*.socpak";
        var extractRoot = await _starBreaker.ExtractAsync(filter, "asteroid-cluster-templates", cancellationToken);
        var templates = new Dictionary<string, TemplateBounds>(StringComparer.OrdinalIgnoreCase);

        foreach (var socpak in Directory.EnumerateFiles(extractRoot, "cluster_modular_warm_*.socpak", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileNameWithoutExtension(socpak);
            try
            {
                using var zip = ZipFile.OpenRead(socpak);
                var xmlEntry = zip.Entries.FirstOrDefault(e => e.FullName.Equals(name + ".xml", StringComparison.OrdinalIgnoreCase));
                if (xmlEntry is null) continue;
                using var reader = new StreamReader(xmlEntry.Open());
                var doc = XDocument.Load(reader);
                var root = doc.Root;
                if (root is null) continue;
                var min = ParseVector3(root.Attribute("minBounds")?.Value);
                var max = ParseVector3(root.Attribute("maxBounds")?.Value);
                var radius = Parse(root.Attribute("radius")?.Value);
                templates[name] = new TemplateBounds(min, max, radius);
            }
            catch (InvalidDataException)
            {
                // Ignore unsupported templates; candidate remains represented as a normal placement.
            }
        }

        var result = new List<SpatialVolume>();
        foreach (var entity in candidates)
        {
            var templateName = Path.GetFileNameWithoutExtension(entity.SourcePath?.Replace('/', Path.DirectorySeparatorChar));
            if (templateName is null || !templates.TryGetValue(templateName, out var bounds))
                continue;

            result.Add(new SpatialVolume(
                Id: $"volume:{entity.Id}",
                Name: entity.Name,
                Type: "AsteroidClusterVolume",
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
                SourceAuthority: "Data.p4k/System ObjectContainer + cluster template ObjectContainer",
                DataStatus: "LOCAL_DIRECT"));
        }

        dataset.Diagnostics.Add($"Asteroid cluster volumes: {result.Count} direct placed volume(s) resolved from shared cluster templates.");
        return result;
    }

    private static double Parse(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0d;

    private static (double X, double Y, double Z) ParseVector3(string? value)
    {
        var parts = (value ?? string.Empty).Split(',');
        return (
            parts.Length > 0 ? Parse(parts[0]) : 0,
            parts.Length > 1 ? Parse(parts[1]) : 0,
            parts.Length > 2 ? Parse(parts[2]) : 0);
    }

    private sealed record TemplateBounds(
        (double X, double Y, double Z) Min,
        (double X, double Y, double Z) Max,
        double Radius);
}

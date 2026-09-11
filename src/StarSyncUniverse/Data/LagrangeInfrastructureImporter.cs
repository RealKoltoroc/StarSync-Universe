using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Data;

/// <summary>
/// Promotes station/rest-stop placements that are direct children of CURRENT LIVE
/// Lagrange-point ObjectContainers into the normal system graph.
/// Geometry always comes from Data.p4k. Cross-build SCUnpacked is used only for a
/// display identity when the same source UUID and the derived world position agree.
/// </summary>
public sealed class LagrangeInfrastructureImporter
{
    private readonly StarBreakerClient _starBreaker;
    private readonly ScUnpackedCatalog _catalog;

    public LagrangeInfrastructureImporter(StarBreakerClient starBreaker, ScUnpackedCatalog catalog)
    {
        _starBreaker = starBreaker;
        _catalog = catalog;
    }

    public async Task<IReadOnlyList<UniverseEntity>> ImportAsync(
        UniverseDataset dataset,
        CancellationToken cancellationToken = default)
    {
        var system = dataset.System.Trim().ToLowerInvariant();
        var lagrangeParents = dataset.Entities
            .Where(e => e.SourcePath?.Replace('\\', '/').Contains($"/system/{system}/lagrangepoints/", StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();
        if (lagrangeParents.Length == 0)
            return [];

        var extractRoot = await _starBreaker.ExtractAsync(
            $"Data/ObjectContainers/PU/system/{system}/lagrangepoints/*.socpak",
            $"{system}-lagrangepoints",
            cancellationToken);
        var existingSourceUuids = dataset.Entities
            .Where(e => !string.IsNullOrWhiteSpace(e.SourceUuid))
            .Select(e => e.SourceUuid!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = new List<UniverseEntity>();

        foreach (var parent in lagrangeParents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var containerName = Path.GetFileNameWithoutExtension(parent.SourcePath?.Replace('/', Path.DirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(containerName)) continue;
            var doc = TryGetRootXml(extractRoot, containerName);
            if (doc?.Root is null) continue;

            foreach (var child in doc.Root.Element("ChildObjectContainers")?.Elements("Child") ?? [])
            {
                var sourcePath = child.Attribute("name")?.Value;
                var sourceUuid = child.Attribute("starMapRecord")?.Value;
                if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(sourceUuid)) continue;
                if (!LooksLikeStation(sourcePath, child.Attribute("entityName")?.Value)) continue;
                if (existingSourceUuids.Contains(sourceUuid)) continue;

                var local = ParseVector3(child.Attribute("pos")?.Value);
                var worldX = parent.X + local.X;
                var worldY = parent.Y + local.Y;
                var worldZ = parent.Z + local.Z;
                var distance = Length(local.X, local.Y, local.Z);
                if (!double.IsFinite(distance)) continue;

                var reference = _catalog.FirstByUuid(sourceUuid);
                var referenceDelta = reference is null || !reference.System.Equals(dataset.System, StringComparison.OrdinalIgnoreCase)
                    ? double.PositiveInfinity
                    : Length(worldX - reference.X, worldY - reference.Y, worldZ - reference.Z);
                var referenceIdentityMatch = reference is not null && referenceDelta <= 1d;
                var name = referenceIdentityMatch
                    ? reference!.Name
                    : LocalUniverseIdentity.DeriveName(dataset.System, child.Attribute("entityName")?.Value, child.Attribute("label")?.Value, sourcePath, sourceUuid);
                var type = referenceIdentityMatch ? reference!.Type : "Manmade";
                var rot = ParseVector4(child.Attribute("rot")?.Value);
                var id = $"lagrange-infra:{(parent.SourceUuid ?? parent.Id).ToLowerInvariant()}:{sourceUuid.ToLowerInvariant()}";
                var canonicalId = $"uuid:{sourceUuid.ToLowerInvariant()}";
                var parentFrameId = $"frame:{parent.Id}";
                var authority = referenceIdentityMatch
                    ? "Data.p4k CURRENT LIVE direct Lagrange child XYZ/UUID/path; SCUnpacked cross-build UUID+world-position matched display identity only"
                    : "Data.p4k CURRENT LIVE direct Lagrange child XYZ/UUID/path";
                var status = referenceIdentityMatch ? "LOCAL_DIRECT_GEOMETRY_REFERENCE_IDENTITY" : "LOCAL_DIRECT";

                var entity = new UniverseEntity(
                    id, sourceUuid, name, type, dataset.System,
                    parent.Id, parent.SourceUuid, sourcePath, child.Attribute("class")?.Value,
                    false, referenceIdentityMatch && reference!.QtValid,
                    worldX, worldY, worldZ,
                    rot.A, rot.B, rot.C, rot.D,
                    distance, authority, status);
                dataset.Entities.Add(entity);
                added.Add(entity);
                existingSourceUuids.Add(sourceUuid);

                if (!dataset.CanonicalNodes.Any(n => n.CanonicalId.Equals(canonicalId, StringComparison.OrdinalIgnoreCase)))
                    dataset.CanonicalNodes.Add(new CanonicalSpatialNode(
                        canonicalId, sourceUuid, name, type, dataset.System, parent.SourceUuid, authority, status));

                dataset.Frames.Add(new SpatialFrameRecord(
                    $"frame:{id}", parentFrameId, "ObjectContainer", canonicalId, id, authority, status));
                dataset.Placements.Add(new SpatialPlacementRecord(
                    id, canonicalId, parent.Id, parentFrameId,
                    local.X, local.Y, local.Z,
                    worldX, worldY, worldZ,
                    rot.A, rot.B, rot.C, rot.D,
                    "STATIC_SNAPSHOT", "PARENT_RELATIVE_SYSTEM_AXES", sourcePath,
                    authority, status));

                dataset.Diagnostics.Add(
                    $"Lagrange infrastructure: {name} at {parent.Name}; local offset={distance:N3} m; UUID={sourceUuid}; referenceDelta={(double.IsFinite(referenceDelta) ? referenceDelta.ToString("N6", CultureInfo.InvariantCulture) : "n/a")} m; geometry=LOCAL_DIRECT current Data.p4k.");
            }
        }

        return added;
    }

    private static XDocument? TryGetRootXml(string extractRoot, string containerName)
    {
        var socpak = Directory.EnumerateFiles(extractRoot, containerName + ".socpak", SearchOption.AllDirectories).FirstOrDefault();
        if (socpak is null) return null;
        using var zip = ZipFile.OpenRead(socpak);
        var expected = containerName + ".xml";
        var entry = zip.Entries.FirstOrDefault(e => e.FullName.Equals(expected, StringComparison.OrdinalIgnoreCase))
            ?? zip.Entries.FirstOrDefault(e => e.FullName.EndsWith("/" + expected, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return null;
        using var stream = entry.Open();
        return XDocument.Load(stream, LoadOptions.None);
    }

    private static bool LooksLikeStation(string path, string? entityName)
    {
        var p = path.Replace('\\', '/');
        return p.Contains("/station/", StringComparison.OrdinalIgnoreCase) ||
               p.Contains("reststop", StringComparison.OrdinalIgnoreCase) ||
               (entityName?.Contains("_RR_", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static (double X, double Y, double Z) ParseVector3(string? value)
    {
        var p = Parse(value, 3);
        return (p[0], p[1], p[2]);
    }

    private static (double A, double B, double C, double D) ParseVector4(string? value)
    {
        var p = Parse(value, 4);
        if (p.All(v => Math.Abs(v) < double.Epsilon)) return (1, 0, 0, 0);
        return (p[0], p[1], p[2], p[3]);
    }

    private static double[] Parse(string? value, int count)
    {
        var result = new double[count];
        if (string.IsNullOrWhiteSpace(value)) return result;
        var parts = value.Split(',');
        for (var i = 0; i < Math.Min(parts.Length, count); i++)
            _ = double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]);
        return result;
    }

    private static double Length(double x, double y, double z) => Math.Sqrt(x * x + y * y + z * z);
}

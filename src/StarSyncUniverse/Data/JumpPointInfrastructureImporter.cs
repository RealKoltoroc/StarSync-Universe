using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Data;

/// <summary>
/// Promotes identity-bearing CURRENT LIVE infrastructure nested inside jump-point SOCs.
/// Position/rotation always come from the current Data.p4k jump-point ObjectContainer.
/// SCUnpacked may provide display identity/QT metadata only after UUID + derived world XYZ
/// match within the strict tolerance used by the rest of StarSyncUniverse.
/// </summary>
public sealed class JumpPointInfrastructureImporter
{
    private readonly StarBreakerClient _starBreaker;
    private readonly ScUnpackedCatalog _catalog;

    public JumpPointInfrastructureImporter(StarBreakerClient starBreaker, ScUnpackedCatalog catalog)
    {
        _starBreaker = starBreaker;
        _catalog = catalog;
    }

    public async Task<IReadOnlyList<UniverseEntity>> ImportAsync(
        UniverseDataset dataset,
        CancellationToken cancellationToken = default)
    {
        var jumpPoints = dataset.Entities
            .Where(e => (e.SourcePath ?? string.Empty).Replace('\\', '/').Contains("/jumppoints/", StringComparison.OrdinalIgnoreCase))
            .Where(e => (e.SourcePath ?? string.Empty).EndsWith(".socpak", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (jumpPoints.Length == 0) return [];

        var promoted = new List<UniverseEntity>();
        var ids = dataset.Entities.Select(e => e.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var strictMatches = 0;
        var maxDelta = 0d;

        foreach (var jumpPoint in jumpPoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourcePath = jumpPoint.SourcePath!;
            var fileName = Path.GetFileName(sourcePath.Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(fileName)) continue;

            XDocument? doc;
            try
            {
                var extractRoot = await _starBreaker.ExtractAsync(
                    $"**/{fileName}",
                    $"jump-infra-{Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant()}",
                    cancellationToken,
                    convert: "cryxml");
                var normalizedSource = sourcePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                var candidates = Directory.EnumerateFiles(extractRoot, fileName, SearchOption.AllDirectories).ToArray();
                var socpak = candidates.FirstOrDefault(p => p.EndsWith(normalizedSource, StringComparison.OrdinalIgnoreCase))
                    ?? candidates.FirstOrDefault(p => p.Replace('\\', '/').Contains("/PU/system/", StringComparison.OrdinalIgnoreCase));
                if (socpak is null) continue;
                using var zip = ZipFile.OpenRead(socpak);
                var expected = Path.GetFileNameWithoutExtension(fileName) + ".xml";
                var entry = zip.Entries.FirstOrDefault(e => e.FullName.Equals(expected, StringComparison.OrdinalIgnoreCase))
                    ?? zip.Entries.FirstOrDefault(e => e.FullName.EndsWith("/" + expected, StringComparison.OrdinalIgnoreCase));
                if (entry is null) continue;
                using var stream = entry.Open();
                doc = XDocument.Load(stream, LoadOptions.None);
            }
            catch (InvalidDataException) { continue; }
            catch (System.Xml.XmlException) { continue; }

            foreach (var child in doc.Root?.Element("ChildObjectContainers")?.Elements("Child") ?? [])
            {
                cancellationToken.ThrowIfCancellationRequested();
                var childPath = child.Attribute("name")?.Value ?? string.Empty;
                var sourceUuid = child.Attribute("starMapRecord")?.Value;
                var entityClass = child.Attribute("class")?.Value ?? string.Empty;
                var entityName = child.Attribute("entityName")?.Value ?? string.Empty;
                if (!IsPromotableInfrastructure(childPath, sourceUuid, entityClass, entityName)) continue;

                var local = ParseVector3(child.Attribute("pos")?.Value);
                var worldX = jumpPoint.X + local.X;
                var worldY = jumpPoint.Y + local.Y;
                var worldZ = jumpPoint.Z + local.Z;
                var reference = _catalog.MatchByUuidAndWorld(sourceUuid, dataset.System, worldX, worldY, worldZ, 1d);
                if (reference is not null)
                {
                    var delta = Length(worldX - reference.X, worldY - reference.Y, worldZ - reference.Z);
                    strictMatches++;
                    maxDelta = Math.Max(maxDelta, delta);
                }

                var guid = child.Attribute("guid")?.Value;
                var identity = !string.IsNullOrWhiteSpace(sourceUuid) ? sourceUuid : guid;
                if (string.IsNullOrWhiteSpace(identity)) continue;
                var placementId = $"jump-infra:{jumpPoint.Id.ToLowerInvariant()}:{identity.ToLowerInvariant()}";
                if (!ids.Add(placementId)) continue;

                // Without a current star-map UUID, anonymous guides/turrets remain implementation detail.
                // The user-facing catalog gets only identity-bearing station/gateway infrastructure.
                if (string.IsNullOrWhiteSpace(sourceUuid)) continue;

                var rot = ParseVector4(child.Attribute("rot")?.Value);
                var name = reference?.Name ?? LocalUniverseIdentity.DeriveName(
                    dataset.System, entityName, child.Attribute("label")?.Value, childPath, identity);
                var type = reference?.Type ?? "Manmade";
                var distance = Length(local.X, local.Y, local.Z);
                var authority = reference is null
                    ? "Data.p4k CURRENT LIVE jump-point child XYZ/UUID/path"
                    : "Data.p4k CURRENT LIVE jump-point child XYZ/UUID/path; SCUnpacked cross-build UUID+world-position matched identity/QT metadata only";
                var status = reference is null ? "LOCAL_DIRECT" : "LOCAL_DIRECT_GEOMETRY_REFERENCE_IDENTITY";

                var entity = new UniverseEntity(
                    placementId, sourceUuid, name, type, dataset.System,
                    jumpPoint.Id, jumpPoint.SourceUuid,
                    childPath, entityClass,
                    reference?.Hidden ?? false, reference?.QtValid ?? false,
                    worldX, worldY, worldZ,
                    rot.A, rot.B, rot.C, rot.D,
                    distance, authority, status);
                dataset.Entities.Add(entity);
                promoted.Add(entity);

                var canonicalId = $"uuid:{sourceUuid.ToLowerInvariant()}";
                if (!dataset.CanonicalNodes.Any(n => n.CanonicalId.Equals(canonicalId, StringComparison.OrdinalIgnoreCase)))
                    dataset.CanonicalNodes.Add(new CanonicalSpatialNode(
                        canonicalId, sourceUuid, name, type, dataset.System,
                        reference?.ParentUuid ?? jumpPoint.SourceUuid, authority, status));

                var parentFrameId = $"frame:{jumpPoint.Id}";
                dataset.Frames.Add(new SpatialFrameRecord(
                    $"frame:{placementId}", parentFrameId, "ObjectContainer", canonicalId, placementId,
                    authority, status));
                dataset.Placements.Add(new SpatialPlacementRecord(
                    placementId, canonicalId, jumpPoint.Id, parentFrameId,
                    local.X, local.Y, local.Z,
                    worldX, worldY, worldZ,
                    rot.A, rot.B, rot.C, rot.D,
                    "STATIC_SNAPSHOT", "PARENT_RELATIVE_JUMPPOINT_AXES", childPath,
                    authority, status));
            }
        }

        dataset.Diagnostics.Add(
            $"Jump-point infrastructure proof: roots={jumpPoints.Length}; promoted={promoted.Count}; strict UUID+world reference matches={strictMatches}; max delta={maxDelta.ToString("N6", CultureInfo.InvariantCulture)} m; geometry remains CURRENT LIVE Data.p4k.");
        return promoted;
    }

    private static bool IsPromotableInfrastructure(string path, string? sourceUuid, string entityClass, string entityName)
    {
        if (string.IsNullOrWhiteSpace(sourceUuid)) return false;
        var p = path.Replace('\\', '/');
        return entityClass.Contains("LocationObjectContainer", StringComparison.OrdinalIgnoreCase)
            && (p.Contains("/station/", StringComparison.OrdinalIgnoreCase)
                || p.Contains("reststop", StringComparison.OrdinalIgnoreCase)
                || entityName.Contains("station", StringComparison.OrdinalIgnoreCase)
                || entityName.Contains("gateway", StringComparison.OrdinalIgnoreCase));
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

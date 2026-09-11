using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Data;

public sealed class BodyOrbitalInfrastructureImporter
{
    private readonly StarBreakerClient _starBreaker;
    private readonly ScUnpackedCatalog _catalog;

    public BodyOrbitalInfrastructureImporter(StarBreakerClient starBreaker, ScUnpackedCatalog catalog)
    {
        _starBreaker = starBreaker;
        _catalog = catalog;
    }

    public async Task<IReadOnlyList<UniverseEntity>> ImportAsync(
        UniverseDataset dataset,
        CancellationToken cancellationToken = default)
    {
        var added = new List<UniverseEntity>();
        var existingSourceUuids = dataset.Entities
            .Where(x => !string.IsNullOrWhiteSpace(x.SourceUuid))
            .Select(x => x.SourceUuid!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var system = dataset.System.Trim().ToLowerInvariant();
        var extractRoot = await _starBreaker.ExtractAsync(
            $"Data/ObjectContainers/PU/system/{system}/{system}*.socpak",
            $"{system}-bodies",
            cancellationToken);

        foreach (var body in dataset.Bodies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(body.SourceUuid) || string.IsNullOrWhiteSpace(body.SourcePath))
                continue;

            var parent = dataset.Entities.FirstOrDefault(e =>
                string.Equals(e.SourceUuid, body.SourceUuid, StringComparison.OrdinalIgnoreCase));
            if (parent is null)
                continue;

            XDocument? root;
            try
            {
                root = TryGetRootXml(extractRoot, body.ContainerName);
            }
            catch
            {
                continue;
            }
            if (root?.Root is null)
                continue;

            foreach (var child in root.Root.Element("ChildObjectContainers")?.Elements("Child") ?? [])
            {
                var sourcePath = child.Attribute("name")?.Value;
                var sourceUuid = child.Attribute("starMapRecord")?.Value;
                if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(sourceUuid))
                    continue;
                if (!LooksLikeOrbitalInfrastructure(sourcePath, child.Attribute("entityName")?.Value))
                    continue;
                if (existingSourceUuids.Contains(sourceUuid))
                    continue;

                var local = ParseVector3(child.Attribute("pos")?.Value);
                var distance = Length(local.X, local.Y, local.Z);
                if (!double.IsFinite(distance) || distance <= body.RadiusMeters)
                    continue;

                var reference = _catalog.FirstByUuid(sourceUuid);
                var referenceIdentityMatch = reference is not null &&
                    reference.System.Equals(dataset.System, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(reference.ParentUuid, body.SourceUuid, StringComparison.OrdinalIgnoreCase);

                var name = referenceIdentityMatch
                    ? reference!.Name
                    : LocalUniverseIdentity.DeriveName(dataset.System, child.Attribute("entityName")?.Value, child.Attribute("label")?.Value, sourcePath, child.Attribute("guid")?.Value ?? sourceUuid);
                var type = referenceIdentityMatch ? reference!.Type : "Manmade";
                var rot = ParseVector4(child.Attribute("rot")?.Value);
                // Body SOCs may reuse the same template instance GUID across different parents.
                // Placement identity must therefore be parent/source scoped, not raw Child.guid scoped.
                var id = $"body-infra:{body.SourceUuid!.ToLowerInvariant()}:{sourceUuid.ToLowerInvariant()}";
                var worldX = parent.X + local.X;
                var worldY = parent.Y + local.Y;
                var worldZ = parent.Z + local.Z;
                var canonicalId = $"uuid:{sourceUuid.ToLowerInvariant()}";
                var parentFrameId = $"frame:{parent.Id}";
                var metadataStatus = referenceIdentityMatch
                    ? "SCUNPACKED_REFERENCE_IDENTITY_UUID_PARENT_MATCH_CROSS_BUILD"
                    : "LOCAL_IDENTITY_ONLY";
                var authority = referenceIdentityMatch
                    ? "Data.p4k CURRENT LIVE direct body child XYZ/UUID/path; SCUnpacked cross-build UUID+parent matched display identity only"
                    : "Data.p4k CURRENT LIVE direct body child XYZ/UUID/path";
                var status = referenceIdentityMatch
                    ? "LOCAL_DIRECT_GEOMETRY_REFERENCE_IDENTITY"
                    : "LOCAL_DIRECT";

                var entity = new UniverseEntity(
                    Id: id,
                    SourceUuid: sourceUuid,
                    Name: name,
                    Type: type,
                    System: dataset.System,
                    ParentId: parent.Id,
                    ParentSourceUuid: body.SourceUuid,
                    SourcePath: sourcePath,
                    EntityClass: child.Attribute("class")?.Value,
                    Hidden: false,
                    QuantumTravelValid: referenceIdentityMatch && reference!.QtValid,
                    X: worldX,
                    Y: worldY,
                    Z: worldZ,
                    Q0: rot.A,
                    Q1: rot.B,
                    Q2: rot.C,
                    Q3: rot.D,
                    DistanceToParentMeters: distance,
                    SourceAuthority: authority,
                    DataStatus: status);
                dataset.Entities.Add(entity);
                added.Add(entity);
                existingSourceUuids.Add(sourceUuid);

                if (!dataset.CanonicalNodes.Any(n => n.CanonicalId.Equals(canonicalId, StringComparison.OrdinalIgnoreCase)))
                    dataset.CanonicalNodes.Add(new CanonicalSpatialNode(
                        canonicalId, sourceUuid, name, type, dataset.System, body.SourceUuid,
                        authority, status));

                dataset.Frames.Add(new SpatialFrameRecord(
                    $"frame:{id}", parentFrameId, "ObjectContainer", canonicalId, id,
                    authority, status));
                dataset.Placements.Add(new SpatialPlacementRecord(
                    id, canonicalId, parent.Id, parentFrameId,
                    local.X, local.Y, local.Z,
                    worldX, worldY, worldZ,
                    rot.A, rot.B, rot.C, rot.D,
                    "STATIC_SNAPSHOT", "PARENT_RELATIVE_SYSTEM_AXES", sourcePath,
                    authority, status));

                dataset.Diagnostics.Add(
                    $"Orbital infrastructure: {name} around {body.Name}; radius={distance:N3} m; UUID={sourceUuid}; identity={metadataStatus}; geometry=LOCAL_DIRECT current Data.p4k.");
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

    private static bool LooksLikeOrbitalInfrastructure(string path, string? entityName)
    {
        var p = path.Replace('\\', '/');
        return p.Contains("/station/", StringComparison.OrdinalIgnoreCase) ||
               p.Contains("reststop", StringComparison.OrdinalIgnoreCase) ||
               (entityName?.Contains("RestStop", StringComparison.OrdinalIgnoreCase) ?? false);
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

using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Data;

/// <summary>
/// Resolves spatially relevant CURRENT LIVE child ObjectContainers used by Nyx's
/// newer carrier/segment layout. Geometry always comes from Data.p4k. Cross-build
/// SCUnpacked is permitted only as identity/QT metadata after UUID + world XYZ match.
/// </summary>
public sealed class HierarchicalInfrastructureImporter
{
    private const int MaxDepth = 2;
    private const int MaxContainers = 96;
    private readonly StarBreakerClient _starBreaker;
    private readonly ScUnpackedCatalog _catalog;

    public HierarchicalInfrastructureImporter(StarBreakerClient starBreaker, ScUnpackedCatalog catalog)
    {
        _starBreaker = starBreaker;
        _catalog = catalog;
    }

    public async Task<IReadOnlyList<UniverseEntity>> ImportAsync(UniverseDataset dataset, CancellationToken cancellationToken = default)
    {
        if (!dataset.System.Equals("nyx", StringComparison.OrdinalIgnoreCase))
            return [];

        var carriers = dataset.Entities
            .Where(IsCarrier)
            .OrderBy(CarrierPriority)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (carriers.Length == 0) return [];

        var xmlCache = new Dictionary<string, XDocument?>(StringComparer.OrdinalIgnoreCase);
        var added = new List<UniverseEntity>();
        var placementIds = dataset.Entities.Select(e => e.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var comparisons = 0;
        var maxDelta = 0d;
        var containersResolved = 0;

        foreach (var carrier in carriers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (containersResolved >= MaxContainers) break;
            if (string.IsNullOrWhiteSpace(carrier.SourcePath)) continue;

            await ExpandAsync(
                dataset,
                carrier,
                carrier.SourcePath,
                carrier.X,
                carrier.Y,
                carrier.Z,
                depth: 0,
                xmlCache,
                placementIds,
                expanded,
                added,
                cancellationToken,
                onComparison: delta => { comparisons++; maxDelta = Math.Max(maxDelta, delta); },
                onContainer: () => containersResolved++);
        }

        dataset.Diagnostics.Add(
            $"Hierarchical infrastructure proof: carriers={carriers.Length}; containersResolved={containersResolved}; promoted={added.Count}; cross-build UUID+world comparisons={comparisons}; max world delta={maxDelta.ToString("N6", CultureInfo.InvariantCulture)} m. CURRENT LIVE Data.p4k remains geometry authority.");
        return added;
    }

    private async Task ExpandAsync(
        UniverseDataset dataset,
        UniverseEntity parent,
        string containerSourcePath,
        double parentWorldX,
        double parentWorldY,
        double parentWorldZ,
        int depth,
        Dictionary<string, XDocument?> xmlCache,
        HashSet<string> placementIds,
        HashSet<string> expanded,
        List<UniverseEntity> added,
        CancellationToken cancellationToken,
        Action<double> onComparison,
        Action onContainer)
    {
        if (depth > MaxDepth || expanded.Count >= MaxContainers) return;
        var expansionKey = $"{parent.Id}|{containerSourcePath}|{depth}";
        if (!expanded.Add(expansionKey)) return;

        var doc = await GetRootXmlAsync(containerSourcePath, xmlCache, cancellationToken);
        if (doc?.Root is null) return;
        onContainer();

        foreach (var child in doc.Root.Element("ChildObjectContainers")?.Elements("Child") ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourcePath = child.Attribute("name")?.Value;
            if (string.IsNullOrWhiteSpace(sourcePath)) continue;

            var sourceUuid = child.Attribute("starMapRecord")?.Value;
            var entityClass = child.Attribute("class")?.Value;
            var entityName = child.Attribute("entityName")?.Value;
            var local = ParseVector3(child.Attribute("pos")?.Value);
            var worldX = parentWorldX + local.X;
            var worldY = parentWorldY + local.Y;
            var worldZ = parentWorldZ + local.Z;
            var rot = ParseVector4(child.Attribute("rot")?.Value);
            var childGuid = child.Attribute("guid")?.Value;
            var identityKey = !string.IsNullOrWhiteSpace(sourceUuid) ? sourceUuid : childGuid;

            var reference = _catalog.MatchByUuidAndWorld(sourceUuid, dataset.System, worldX, worldY, worldZ, 1d);
            if (reference is not null)
                onComparison(Length(worldX - reference.X, worldY - reference.Y, worldZ - reference.Z));

            UniverseEntity? promoted = null;
            if (ShouldPromote(sourcePath, sourceUuid, entityClass, entityName, reference) && !string.IsNullOrWhiteSpace(identityKey))
            {
                var id = $"hier-infra:{parent.Id.ToLowerInvariant()}:{identityKey.ToLowerInvariant()}";
                if (placementIds.Add(id))
                {
                    var name = reference?.Name ?? LocalUniverseIdentity.DeriveName(dataset.System, entityName, child.Attribute("label")?.Value, sourcePath, identityKey);
                    var type = reference?.Type ?? InferLocalType(sourcePath, entityClass, entityName);
                    var canonicalId = !string.IsNullOrWhiteSpace(sourceUuid) ? $"uuid:{sourceUuid.ToLowerInvariant()}" : $"placement-only:{id.ToLowerInvariant()}";
                    var parentFrameId = $"frame:{parent.Id}";
                    var distance = Length(local.X, local.Y, local.Z);
                    var authority = reference is not null
                        ? "Data.p4k CURRENT LIVE hierarchical child XYZ/UUID/path; SCUnpacked cross-build UUID+world-position matched display identity/QT metadata only"
                        : "Data.p4k CURRENT LIVE hierarchical child XYZ/UUID/path";
                    var status = reference is not null ? "LOCAL_DIRECT_GEOMETRY_REFERENCE_IDENTITY" : "LOCAL_DIRECT";

                    promoted = new UniverseEntity(
                        id, sourceUuid, name, type, dataset.System,
                        parent.Id, reference?.ParentUuid ?? parent.SourceUuid,
                        sourcePath, entityClass,
                        reference?.Hidden ?? false, reference?.QtValid ?? false,
                        worldX, worldY, worldZ,
                        rot.A, rot.B, rot.C, rot.D,
                        distance, authority, status);
                    dataset.Entities.Add(promoted);
                    added.Add(promoted);

                    if (!dataset.CanonicalNodes.Any(n => n.CanonicalId.Equals(canonicalId, StringComparison.OrdinalIgnoreCase)))
                        dataset.CanonicalNodes.Add(new CanonicalSpatialNode(canonicalId, sourceUuid, name, type, dataset.System, reference?.ParentUuid ?? parent.SourceUuid, authority, status));

                    dataset.Frames.Add(new SpatialFrameRecord($"frame:{id}", parentFrameId, "ObjectContainer", canonicalId, id, authority, status));
                    dataset.Placements.Add(new SpatialPlacementRecord(
                        id, canonicalId, parent.Id, parentFrameId,
                        local.X, local.Y, local.Z,
                        worldX, worldY, worldZ,
                        rot.A, rot.B, rot.C, rot.D,
                        "STATIC_SNAPSHOT", "PARENT_RELATIVE_SYSTEM_AXES", sourcePath,
                        authority, status));

                    dataset.Diagnostics.Add($"Hierarchical infrastructure: {name}; carrier={parent.Name}; child={entityName}; depth={depth + 1}; UUID={sourceUuid ?? "none"}; localOffset={distance:N3} m; geometry=CURRENT LIVE Data.p4k; reference={(reference is null ? "none" : "UUID+world match")}.");
                }
            }

            if (depth < MaxDepth && ShouldTraverseNested(sourcePath, entityClass, entityName, reference))
            {
                var nextParent = promoted ?? parent;
                await ExpandAsync(
                    dataset,
                    nextParent,
                    sourcePath,
                    worldX,
                    worldY,
                    worldZ,
                    depth + 1,
                    xmlCache,
                    placementIds,
                    expanded,
                    added,
                    cancellationToken,
                    onComparison,
                    onContainer);
            }
        }
    }

    private async Task<XDocument?> GetRootXmlAsync(string sourcePath, Dictionary<string, XDocument?> cache, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(sourcePath, out var cached)) return cached;
        var normalized = sourcePath.Replace('\\', '/');
        var fileName = Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".socpak", StringComparison.OrdinalIgnoreCase))
            return cache[sourcePath] = null;

        try
        {
            var cacheName = "hier-infra-" + Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant();
            var extractRoot = await _starBreaker.ExtractAsync($"**/{fileName}", cacheName, cancellationToken, convert: "cryxml");
            var socpak = Directory.EnumerateFiles(extractRoot, fileName, SearchOption.AllDirectories).FirstOrDefault();
            if (socpak is null) return cache[sourcePath] = null;

            using var zip = ZipFile.OpenRead(socpak);
            var expected = Path.GetFileNameWithoutExtension(fileName) + ".xml";
            var entry = zip.Entries.FirstOrDefault(e => e.FullName.Equals(expected, StringComparison.OrdinalIgnoreCase))
                ?? zip.Entries.FirstOrDefault(e => e.FullName.EndsWith("/" + expected, StringComparison.OrdinalIgnoreCase));
            if (entry is null) return cache[sourcePath] = null;
            using var stream = entry.Open();
            return cache[sourcePath] = XDocument.Load(stream, LoadOptions.None);
        }
        catch (InvalidDataException) { return cache[sourcePath] = null; }
        catch (System.Xml.XmlException) { return cache[sourcePath] = null; }
    }

    private static bool IsCarrier(UniverseEntity entity)
    {
        var path = entity.SourcePath?.Replace('\\', '/') ?? string.Empty;
        if (!path.Contains("/system/nyx/", StringComparison.OrdinalIgnoreCase)) return false;
        return path.Contains("segment_social_", StringComparison.OrdinalIgnoreCase)
            || path.Contains("segment_levski", StringComparison.OrdinalIgnoreCase)
            || path.Contains("segment_rckcrk_", StringComparison.OrdinalIgnoreCase);
    }

    private static int CarrierPriority(UniverseEntity entity)
    {
        var path = entity.SourcePath?.Replace('\\', '/') ?? string.Empty;
        if (path.Contains("segment_social_", StringComparison.OrdinalIgnoreCase)) return 0;
        if (path.Contains("segment_levski", StringComparison.OrdinalIgnoreCase)) return 1;
        return 2;
    }

    private static bool ShouldPromote(string sourcePath, string? sourceUuid, string? entityClass, string? entityName, ScUnpackedEntry? reference)
    {
        // The top-level Universe catalog contains identity-bearing spatial objects only.
        // Anonymous internal elevators/caverns/layout containers remain traversal nodes and
        // must not flood the system catalog. They can later live in a dedicated deep-OC graph.
        return reference is not null || !string.IsNullOrWhiteSpace(sourceUuid);
    }

    private static bool ShouldTraverseNested(string sourcePath, string? entityClass, string? entityName, ScUnpackedEntry? reference)
    {
        var path = sourcePath.Replace('\\', '/');
        if (!path.EndsWith(".socpak", StringComparison.OrdinalIgnoreCase)) return false;
        return reference is not null
            || entityClass?.Contains("LocationObjectContainer", StringComparison.OrdinalIgnoreCase) == true
            || path.Contains("/station/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/levski/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/outpost/", StringComparison.OrdinalIgnoreCase)
            || entityName?.Contains("station", StringComparison.OrdinalIgnoreCase) == true
            || entityName?.Contains("levski", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string InferLocalType(string? sourcePath, string? entityClass, string? entityName)
    {
        var path = sourcePath ?? string.Empty;
        var name = entityName ?? string.Empty;
        if (path.Contains("/levski/", StringComparison.OrdinalIgnoreCase) || name.Contains("levski", StringComparison.OrdinalIgnoreCase)) return "LandingZone";
        if (path.Contains("/outpost/", StringComparison.OrdinalIgnoreCase) || name.Contains("clinic", StringComparison.OrdinalIgnoreCase)) return "Outpost";
        if (path.Contains("/station/", StringComparison.OrdinalIgnoreCase) || name.Contains("station", StringComparison.OrdinalIgnoreCase)) return "Manmade";
        if (entityClass?.Contains("LocationObjectContainer", StringComparison.OrdinalIgnoreCase) == true) return "Manmade";
        return entityClass ?? "ObjectContainer";
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

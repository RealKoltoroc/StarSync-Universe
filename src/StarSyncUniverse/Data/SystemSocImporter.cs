using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Data;

public sealed class SystemSocImporter
{
    private readonly StarBreakerClient _starBreaker;
    private readonly ScUnpackedCatalog? _catalog;

    public SystemSocImporter(StarBreakerClient starBreaker, ScUnpackedCatalog? catalog)
    {
        _starBreaker = starBreaker;
        _catalog = catalog;
    }

    public async Task<UniverseDataset> ImportAsync(string system, CancellationToken cancellationToken = default)
    {
        system = NormalizeSystem(system);
        var filter = $"Data/ObjectContainers/PU/system/{system}/{system}system.socpak";
        var extractRoot = await _starBreaker.ExtractAsync(filter, $"{system}-system", cancellationToken);
        var socpakPath = Directory.EnumerateFiles(extractRoot, $"{system}system.socpak", SearchOption.AllDirectories).Single();

        using var zip = ZipFile.OpenRead(socpakPath);
        var xmlEntry = zip.Entries.Single(e => e.FullName.Equals($"{system}system.xml", StringComparison.OrdinalIgnoreCase));
        using var reader = new StreamReader(xmlEntry.Open());
        var doc = XDocument.Load(reader);

        var detectedBuild = DetectBuild();
        var useScEnrichment = _catalog?.IsBuildCompatible(detectedBuild) == true;
        var dataset = new UniverseDataset
        {
            System = system,
            Build = detectedBuild,
            PrimarySource = filter,
            SecondarySource = useScEnrichment
                ? "scunpacked-data-master/starmap_positions.json (same-build enrichment/validation)"
                : "scunpacked-data-master/starmap_positions.json (build-mismatch validation/reference only)"
        };

        if (_catalog is not null && !useScEnrichment)
            dataset.Diagnostics.Add($"SCUnpacked build guard: source P4={_catalog.SourceP4?.ToString() ?? "unknown"} does not match current {dataset.Build}; catalog is validation/reference only and cannot override local identity/name/type/visibility values.");

        var p4kInfo = new FileInfo(_starBreaker.DataP4k);
        dataset.Provenance.Add(new SourceProvenanceRecord(
            "data.p4k", "DataP4k", p4kInfo.FullName, true,
            p4kInfo.Exists ? p4kInfo.Length : null,
            p4kInfo.Exists ? new DateTimeOffset(p4kInfo.LastWriteTimeUtc, TimeSpan.Zero) : null,
            p4kInfo.Exists ? $"{p4kInfo.Length}:{p4kInfo.LastWriteTimeUtc.Ticks}" : "MISSING",
            "QUICK_SIZE_LASTWRITE", p4kInfo.Exists ? "LOCAL_DIRECT" : "MISSING"));

        var liveRoot = Directory.GetParent(_starBreaker.DataP4k)?.FullName;
        var manifestPath = liveRoot is null ? null : Path.Combine(liveRoot, "build_manifest.id");
        if (manifestPath is not null && File.Exists(manifestPath))
        {
            var manifestInfo = new FileInfo(manifestPath);
            dataset.Provenance.Add(new SourceProvenanceRecord(
                "build_manifest", "GameBuildManifest", manifestInfo.FullName, true,
                manifestInfo.Length,
                new DateTimeOffset(manifestInfo.LastWriteTimeUtc, TimeSpan.Zero),
                await ComputeSha256Async(manifestInfo.FullName, cancellationToken),
                "SHA256", "LOCAL_DIRECT"));
        }

        var socInfo = new FileInfo(socpakPath);
        dataset.Provenance.Add(new SourceProvenanceRecord(
            $"{system}.system.socpak", "ObjectContainer", filter, true,
            socInfo.Length,
            new DateTimeOffset(socInfo.LastWriteTimeUtc, TimeSpan.Zero),
            await ComputeSha256Async(socpakPath, cancellationToken),
            "SHA256_EXTRACTED_SOURCE", "LOCAL_DIRECT"));

        if (_catalog is not null && File.Exists(_catalog.SourcePath))
        {
            var scInfo = new FileInfo(_catalog.SourcePath);
            dataset.Provenance.Add(new SourceProvenanceRecord(
                "scunpacked.starmap_positions", "SCUnpacked", scInfo.FullName, false,
                scInfo.Length,
                new DateTimeOffset(scInfo.LastWriteTimeUtc, TimeSpan.Zero),
                await ComputeSha256Async(scInfo.FullName, cancellationToken),
                "SHA256", useScEnrichment ? "SCUNPACKED_SAME_BUILD_ENRICHMENT_ALLOWED" : "SCUNPACKED_BUILD_MISMATCH_VALIDATION_ONLY"));
        }

        dataset.Frames.Add(new SpatialFrameRecord(
            $"system:{system}", null, "System", null, null,
            "Data.p4k/ObjectContainer", "LOCAL_DIRECT"));

        var star = useScEnrichment ? _catalog?.Entries.FirstOrDefault(x =>
            x.System.Equals(system, StringComparison.OrdinalIgnoreCase) &&
            x.Type.Equals("Star", StringComparison.OrdinalIgnoreCase)) : null;

        if (star is not null)
        {
            var starId = $"system:{system}:star";
            dataset.Entities.Add(new UniverseEntity(
                Id: starId,
                SourceUuid: star.Uuid,
                Name: star.Name,
                Type: star.Type,
                System: system,
                ParentId: null,
                ParentSourceUuid: null,
                SourcePath: filter,
                EntityClass: "Sun",
                Hidden: star.Hidden,
                QuantumTravelValid: star.QtValid,
                X: 0, Y: 0, Z: 0,
                Q0: 1, Q1: 0, Q2: 0, Q3: 0,
                DistanceToParentMeters: 0,
                SourceAuthority: "Data.p4k/ObjectContainer + SCUnpacked identity",
                DataStatus: "LOCAL_DIRECT"));

            var canonicalId = CanonicalId(star.Uuid, starId);
            dataset.CanonicalNodes.Add(new CanonicalSpatialNode(
                canonicalId, star.Uuid, star.Name, star.Type, system, null,
                "Data.p4k/ObjectContainer + SCUnpacked identity", "LOCAL_DIRECT"));
            dataset.Frames.Add(new SpatialFrameRecord(
                $"frame:{starId}", $"system:{system}", "CelestialBodyCenter", canonicalId, starId,
                "Data.p4k/ObjectContainer", "LOCAL_DIRECT"));
            dataset.Placements.Add(new SpatialPlacementRecord(
                starId, canonicalId, null, $"system:{system}",
                0, 0, 0, 0, 0, 0,
                1, 0, 0, 0, "STATIC", "SYSTEM_AXES", filter,
                "Data.p4k/ObjectContainer", "LOCAL_DIRECT"));
        }

        var roots = doc.Root?.Element("ChildObjectContainers")?.Elements("Child") ?? [];
        foreach (var child in roots)
            ParseChild(child, system, null, null, (0d, 0d, 0d), dataset, useScEnrichment);

        dataset.Diagnostics.Add($"Imported {dataset.Entities.Count} {system} spatial entities from current {system}system.socpak.");
        return dataset;
    }

    private void ParseChild(
        XElement child,
        string system,
        string? parentId,
        string? parentSourceUuid,
        (double X, double Y, double Z) parentWorld,
        UniverseDataset dataset,
        bool useScEnrichment)
    {
        var id = child.Attribute("guid")?.Value ?? Guid.NewGuid().ToString("D");
        var sourceUuid = child.Attribute("starMapRecord")?.Value;
        var enrichment = useScEnrichment ? _catalog?.FirstByUuid(sourceUuid) : null;
        var local = ParseVector3(child.Attribute("pos")?.Value);
        var rot = ParseVector4(child.Attribute("rot")?.Value, (1, 0, 0, 0));
        var world = (parentWorld.X + local.X, parentWorld.Y + local.Y, parentWorld.Z + local.Z);
        var localDistance = Math.Sqrt(local.X * local.X + local.Y * local.Y + local.Z * local.Z);
        var entityName = child.Attribute("entityName")?.Value;
        var entityClass = child.Attribute("class")?.Value;
        var sourcePath = child.Attribute("name")?.Value;

        var existingSyntheticRoot = parentId is null &&
            enrichment?.Type.Equals("Star", StringComparison.OrdinalIgnoreCase) == true &&
            dataset.Entities.FirstOrDefault(e => e.Type.Equals("Star", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.SourceUuid, sourceUuid, StringComparison.OrdinalIgnoreCase)) is { } existingStar
                ? existingStar
                : null;

        if (existingSyntheticRoot is null)
        {
            var name = LocalUniverseIdentity.IsUsableDisplayName(enrichment?.Name)
                ? enrichment!.Name
                : LocalUniverseIdentity.DeriveName(system, entityName, child.Attribute("label")?.Value, sourcePath, id);
            var type = enrichment?.Type ?? LocalUniverseIdentity.InferType(system, entityClass, sourcePath, entityName);
            var canonicalId = CanonicalId(sourceUuid, id);

            dataset.Entities.Add(new UniverseEntity(
                Id: id,
                SourceUuid: sourceUuid,
                Name: name,
                Type: type,
                System: system,
                ParentId: parentId,
                ParentSourceUuid: enrichment?.ParentUuid ?? parentSourceUuid,
                SourcePath: sourcePath,
                EntityClass: entityClass,
                Hidden: enrichment?.Hidden ?? false,
                QuantumTravelValid: enrichment?.QtValid ?? false,
                X: world.Item1,
                Y: world.Item2,
                Z: world.Item3,
                Q0: rot.A,
                Q1: rot.B,
                Q2: rot.C,
                Q3: rot.D,
                DistanceToParentMeters: localDistance,
                SourceAuthority: "Data.p4k/ObjectContainer",
                DataStatus: "LOCAL_DIRECT"));

            if (!dataset.CanonicalNodes.Any(n => n.CanonicalId.Equals(canonicalId, StringComparison.OrdinalIgnoreCase)))
            {
                dataset.CanonicalNodes.Add(new CanonicalSpatialNode(
                    canonicalId, sourceUuid, name, type, system,
                    enrichment?.ParentUuid ?? parentSourceUuid,
                    "Data.p4k/ObjectContainer", "LOCAL_DIRECT"));
            }

            var parentFrameId = parentId is null ? $"system:{system}" : $"frame:{parentId}";
            var frameType = type.Equals("Star", StringComparison.OrdinalIgnoreCase) ||
                            type.Equals("Planet", StringComparison.OrdinalIgnoreCase) ||
                            type.Equals("Moon", StringComparison.OrdinalIgnoreCase)
                ? "CelestialBodyCenter"
                : "ObjectContainer";
            dataset.Frames.Add(new SpatialFrameRecord(
                $"frame:{id}", parentFrameId, frameType, canonicalId, id,
                "Data.p4k/ObjectContainer", "LOCAL_DIRECT"));

            dataset.Placements.Add(new SpatialPlacementRecord(
                id, canonicalId, parentId, parentFrameId,
                local.X, local.Y, local.Z,
                world.Item1, world.Item2, world.Item3,
                rot.A, rot.B, rot.C, rot.D,
                "STATIC", "PARENT_RELATIVE_SYSTEM_AXES", sourcePath,
                "Data.p4k/ObjectContainer", "LOCAL_DIRECT"));
        }

        var effectiveId = existingSyntheticRoot?.Id ?? id;
        var nested = child.Element("ChildObjectContainers")?.Elements("Child") ?? [];
        foreach (var nestedChild in nested)
            ParseChild(nestedChild, system, effectiveId, sourceUuid ?? parentSourceUuid, world, dataset, useScEnrichment);
    }

    private static string CanonicalId(string? sourceUuid, string placementId) =>
        !string.IsNullOrWhiteSpace(sourceUuid)
            ? $"uuid:{sourceUuid.ToLowerInvariant()}"
            : $"placement-only:{placementId.ToLowerInvariant()}";

    private static string NormalizeSystem(string system)
    {
        var normalized = system.Trim().ToLowerInvariant();
        return normalized is "stanton" or "pyro" or "nyx"
            ? normalized
            : throw new ArgumentOutOfRangeException(nameof(system), system, "Supported systems are Stanton, Pyro and Nyx.");
    }

    private static (double X, double Y, double Z) ParseVector3(string? value)
    {
        var p = Split(value, 3);
        return (p[0], p[1], p[2]);
    }

    private static (double A, double B, double C, double D) ParseVector4(string? value, (double, double, double, double) fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        var p = Split(value, 4);
        return (p[0], p[1], p[2], p[3]);
    }

    private static double[] Split(string? value, int count)
    {
        if (string.IsNullOrWhiteSpace(value)) return new double[count];
        var parts = value.Split(',');
        var result = new double[count];
        for (var i = 0; i < Math.Min(parts.Length, count); i++)
            _ = double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]);
        return result;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan | FileOptions.Asynchronous);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string DetectBuild()
    {
        var liveRoot = Directory.GetParent(_starBreaker.DataP4k)?.FullName;
        if (liveRoot is null) return "LIVE";

        var manifestPath = Path.Combine(liveRoot, "build_manifest.id");
        if (!File.Exists(manifestPath)) return $"LIVE:{liveRoot}";

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var data = document.RootElement.GetProperty("Data");
            var branch = data.TryGetProperty("Branch", out var b) ? b.GetString() : null;
            var p4 = data.TryGetProperty("RequestedP4ChangeNum", out var p) ? p.GetString() : null;
            var buildId = data.TryGetProperty("BuildId", out var i) ? i.GetString() : null;
            var version = data.TryGetProperty("Version", out var v) ? v.GetString() : null;
            return $"{branch ?? "LIVE"}|P4={p4 ?? "?"}|BuildId={buildId ?? "?"}|Version={version ?? "?"}";
        }
        catch
        {
            return $"LIVE:{liveRoot}";
        }
    }
}

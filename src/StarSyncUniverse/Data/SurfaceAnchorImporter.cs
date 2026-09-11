using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using StarSyncUniverse.Domain;
using StarSyncUniverse.Transforms;

namespace StarSyncUniverse.Data;

public sealed class SurfaceAnchorImporter
{
    private readonly StarBreakerClient _starBreaker;
    private readonly ScUnpackedCatalog? _catalog;

    public SurfaceAnchorImporter(StarBreakerClient starBreaker, ScUnpackedCatalog? catalog)
    {
        _starBreaker = starBreaker;
        _catalog = catalog;
    }

    public async Task<IReadOnlyList<BodyLocalAnchorRecord>> ImportAsync(
        UniverseDataset dataset,
        CancellationToken cancellationToken = default)
    {
        var system = dataset.System.Trim().ToLowerInvariant();
        var filter = $"Data/ObjectContainers/PU/system/{system}/{system}*.socpak";
        var extractRoot = await _starBreaker.ExtractAsync(filter, $"{system}-bodies", cancellationToken);
        var results = new List<BodyLocalAnchorRecord>();

        foreach (var body in dataset.Bodies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var socpak = Directory.EnumerateFiles(extractRoot, body.ContainerName + ".socpak", SearchOption.AllDirectories).FirstOrDefault();
            if (socpak is null || body.RadiusMeters <= 0) continue;

            try
            {
                using var zip = ZipFile.OpenRead(socpak);
                var xmlEntry = zip.Entries.FirstOrDefault(e => e.FullName.Equals(body.ContainerName + ".xml", StringComparison.OrdinalIgnoreCase));
                if (xmlEntry is null) continue;
                using var reader = new StreamReader(xmlEntry.Open());
                var doc = XDocument.Load(reader);
                var bodyEntity = dataset.Entities.FirstOrDefault(e =>
                    (!string.IsNullOrWhiteSpace(body.SourceUuid) && string.Equals(e.SourceUuid, body.SourceUuid, StringComparison.OrdinalIgnoreCase)) ||
                    (e.SourcePath?.Replace('\\', '/').EndsWith('/' + body.ContainerName + ".socpak", StringComparison.OrdinalIgnoreCase) ?? false));
                var bodyFixedFrameId = bodyEntity is null
                    ? $"bodyfixed:{system}:{body.ContainerName}"
                    : $"frame:{bodyEntity.Id}:bodyfixed";
                var roots = doc.Root?.Element("ChildObjectContainers")?.Elements("Child") ?? [];
                var useScEnrichment = _catalog?.IsBuildCompatible(dataset.Build) == true;
                foreach (var child in roots)
                    ParseChild(child, Vector3D.Zero, body, bodyFixedFrameId, system, results, useScEnrichment);
            }
            catch (InvalidDataException)
            {
                // Keep unsupported containers out of the derived surface index.
            }
        }

        return results
            .GroupBy(x => x.AnchorId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.BodyName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void ParseChild(
        XElement child,
        Vector3D parentLocal,
        CelestialBodyPhysical body,
        string bodyFixedFrameId,
        string system,
        List<BodyLocalAnchorRecord> results,
        bool useScEnrichment)
    {
        var local = ParseVector3(child.Attribute("pos")?.Value);
        var bodyLocal = parentLocal + local;
        var sourceUuid = child.Attribute("starMapRecord")?.Value;
        var entityClass = child.Attribute("class")?.Value;
        var entityName = child.Attribute("entityName")?.Value;
        var sourcePath = child.Attribute("name")?.Value;
        var id = child.Attribute("guid")?.Value ?? sourceUuid ?? entityName;

        var isAnchor = !string.IsNullOrWhiteSpace(sourceUuid) ||
                       string.Equals(entityClass, "LocationObjectContainer", StringComparison.OrdinalIgnoreCase);
        if (isAnchor && !string.IsNullOrWhiteSpace(id) && bodyLocal.Length > body.RadiusMeters * 0.5d)
        {
            var spherical = SurfaceCoordinateEngine.CartesianToLatLonAlt(bodyLocal, body.RadiusMeters);
            var enrichment = useScEnrichment ? _catalog?.FirstByUuid(sourceUuid) : null;
            var anchorScope = spherical.AltitudeMeters switch
            {
                < -50_000d => "BODY_LOCAL_SUBSURFACE",
                <= 50_000d => "NEAR_SURFACE",
                <= 250_000d => "BODY_LOCAL_ATMOSPHERIC_OR_ELEVATED",
                _ => "BODY_LOCAL_SPACE_ORBITAL"
            };
            results.Add(new BodyLocalAnchorRecord(
                AnchorId: id,
                System: system,
                BodyName: body.Name,
                BodySourceUuid: body.SourceUuid,
                BodyFixedFrameId: bodyFixedFrameId,
                Name: LocalUniverseIdentity.IsUsableDisplayName(enrichment?.Name)
                    ? enrichment!.Name
                    : LocalUniverseIdentity.DeriveName(system, entityName, child.Attribute("label")?.Value, sourcePath, id),
                SourceUuid: sourceUuid,
                EntityClass: entityClass,
                BodyLocalX: bodyLocal.X,
                BodyLocalY: bodyLocal.Y,
                BodyLocalZ: bodyLocal.Z,
                ReferenceRadiusMeters: body.RadiusMeters,
                AxisLatitudeDegrees: spherical.LatitudeDegrees,
                AxisLongitudeDegrees: spherical.LongitudeDegrees,
                AltitudeMeters: spherical.AltitudeMeters,
                AnchorScope: anchorScope,
                CoordinateConvention: "BODY_LOCAL_ENGINE_AXES_SPHERICAL_GEODETIC_CONVENTION_UNPROVEN",
                SourcePath: sourcePath ?? body.SourcePath,
                SourceAuthority: "Data.p4k/Body ObjectContainer + LOCAL_DERIVED spherical projection",
                DataStatus: "LOCAL_DIRECT_XYZ_LOCAL_DERIVED_SPHERICAL"));
        }

        var nested = child.Element("ChildObjectContainers")?.Elements("Child") ?? [];
        foreach (var nestedChild in nested)
            ParseChild(nestedChild, bodyLocal, body, bodyFixedFrameId, system, results, useScEnrichment);
    }

    private static Vector3D ParseVector3(string? value)
    {
        var parts = (value ?? string.Empty).Split(',');
        return new Vector3D(
            parts.Length > 0 ? Parse(parts[0]) : 0,
            parts.Length > 1 ? Parse(parts[1]) : 0,
            parts.Length > 2 ? Parse(parts[2]) : 0);
    }

    private static double Parse(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0d;
}

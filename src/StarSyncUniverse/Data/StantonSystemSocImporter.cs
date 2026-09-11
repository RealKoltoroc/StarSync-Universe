using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Data;

public sealed class StantonSystemSocImporter
{
    private readonly StarBreakerClient _starBreaker;
    private readonly ScUnpackedCatalog? _catalog;

    public StantonSystemSocImporter(StarBreakerClient starBreaker, ScUnpackedCatalog? catalog)
    {
        _starBreaker = starBreaker;
        _catalog = catalog;
    }

    public async Task<UniverseDataset> ImportAsync(CancellationToken cancellationToken = default)
    {
        const string filter = "Data/ObjectContainers/PU/system/stanton/stantonsystem.socpak";
        var extractRoot = await _starBreaker.ExtractAsync(filter, "stanton-system", cancellationToken);
        var socpakPath = Directory.EnumerateFiles(extractRoot, "stantonsystem.socpak", SearchOption.AllDirectories).Single();

        using var zip = ZipFile.OpenRead(socpakPath);
        var xmlEntry = zip.Entries.Single(e => e.FullName.Equals("stantonsystem.xml", StringComparison.OrdinalIgnoreCase));
        using var reader = new StreamReader(xmlEntry.Open());
        var doc = XDocument.Load(reader);

        var dataset = new UniverseDataset
        {
            System = "stanton",
            Build = DetectBuild(),
            PrimarySource = filter,
            SecondarySource = "scunpacked-data-master/starmap_positions.json (enrichment/validation only)"
        };

        var star = _catalog?.Entries.FirstOrDefault(x => x.System.Equals("stanton", StringComparison.OrdinalIgnoreCase) && x.Type.Equals("Star", StringComparison.OrdinalIgnoreCase));
        if (star is not null)
        {
            dataset.Entities.Add(new UniverseEntity(
                Id: "system:stanton:star",
                SourceUuid: star.Uuid,
                Name: star.Name,
                Type: star.Type,
                System: "stanton",
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
        }

        var roots = doc.Root?.Element("ChildObjectContainers")?.Elements("Child") ?? [];
        foreach (var child in roots)
            ParseChild(child, null, null, (0d, 0d, 0d), dataset);

        dataset.Diagnostics.Add($"Imported {dataset.Entities.Count} Stanton spatial entities from current stantonsystem.socpak.");
        return dataset;
    }

    private void ParseChild(
        XElement child,
        string? parentId,
        string? parentSourceUuid,
        (double X, double Y, double Z) parentWorld,
        UniverseDataset dataset)
    {
        var id = child.Attribute("guid")?.Value ?? Guid.NewGuid().ToString("D");
        var sourceUuid = child.Attribute("starMapRecord")?.Value;
        var enrichment = _catalog?.FirstByUuid(sourceUuid);
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
            dataset.Entities.Add(new UniverseEntity(
                Id: id,
                SourceUuid: sourceUuid,
                Name: LocalUniverseIdentity.IsUsableDisplayName(enrichment?.Name)
                    ? enrichment!.Name
                    : LocalUniverseIdentity.DeriveName("stanton", entityName, child.Attribute("label")?.Value, sourcePath, id),
                Type: enrichment?.Type ?? entityClass ?? "ObjectContainer",
                System: "stanton",
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
        }

        var effectiveId = existingSyntheticRoot?.Id ?? id;
        var nested = child.Element("ChildObjectContainers")?.Elements("Child") ?? [];
        foreach (var nestedChild in nested)
            ParseChild(nestedChild, effectiveId, sourceUuid ?? parentSourceUuid, world, dataset);
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

    private string DetectBuild()
    {
        var liveRoot = Directory.GetParent(_starBreaker.DataP4k)?.FullName;
        return liveRoot is null ? "LIVE" : $"LIVE:{liveRoot}";
    }
}

using System.Text.Json;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class UniverseSnapshotReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<IReadOnlyDictionary<string, UniverseDataset>> LoadAllAsync(
        CancellationToken cancellationToken = default)
    {
        var bundledRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "CommunityBaseline", "Universe");
        var localRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StarSyncUniverse", "Snapshots");

        var result = new Dictionary<string, UniverseDataset>(StringComparer.OrdinalIgnoreCase);

        // The packaged baseline is the no-tool first-start source. A user-authorized LIVE refresh writes
        // local snapshots which override the packaged system snapshot without making them mandatory.
        await LoadRootAsync(bundledRoot, "PACKAGED_COMMUNITY_BASELINE", result, cancellationToken);
        await LoadRootAsync(localRoot, "LOCAL_REFRESHED_SNAPSHOT", result, cancellationToken);

        if (result.Count == 0)
            throw new InvalidDataException(
                $"No usable StarSyncUniverse snapshots were found. Packaged baseline: '{bundledRoot}'. Local cache: '{localRoot}'.");
        return result;
    }

    private static async Task LoadRootAsync(
        string root,
        string sourceKind,
        Dictionary<string, UniverseDataset> target,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root)) return;
        foreach (var path in Directory.EnumerateFiles(root, "*.current.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Path.GetFileName(path).Equals("universe.current.json", StringComparison.OrdinalIgnoreCase))
                continue;
            var dataset = await LoadAsync(path, cancellationToken);
            dataset.Diagnostics.Add($"Snapshot source kind: {sourceKind} · {path}");
            target[dataset.System] = dataset;
        }
    }

    public static async Task<UniverseDataset> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = json.RootElement;

        var dataset = new UniverseDataset
        {
            System = RequiredString(root, "System"),
            Build = RequiredString(root, "Build"),
            PrimarySource = OptionalString(root, "PrimarySource") ?? "STARSYNC_SNAPSHOT",
            SecondarySource = OptionalString(root, "SecondarySource"),
            GalaxyX = OptionalDouble(root, "GalaxyX"),
            GalaxyY = OptionalDouble(root, "GalaxyY"),
            GalaxyZ = OptionalDouble(root, "GalaxyZ"),
            GalaxyPositionStatus = OptionalString(root, "GalaxyPositionStatus") ?? "SNAPSHOT_LEGACY_GALAXY_POSITION_UNAVAILABLE",
            GalaxyPositionAuthority = OptionalString(root, "GalaxyPositionAuthority") ?? "SNAPSHOT"
        };

        AddList(root, "entities", dataset.Entities);
        AddList(root, "canonicalNodes", dataset.CanonicalNodes);
        AddList(root, "frames", dataset.Frames);
        AddList(root, "placements", dataset.Placements);
        AddList(root, "bodies", dataset.Bodies);
        AddList(root, "simulatedOrbits", dataset.SimulatedOrbits);
        AddList(root, "temporalTransforms", dataset.TemporalTransforms);
        AddList(root, "bodyAnchors", dataset.BodyAnchors);
        AddList(root, "orientationEvidence", dataset.OrientationEvidence);
        AddList(root, "surfaceCoverage", dataset.SurfaceCoverage);
        AddList(root, "rotationPhaseEvidence", dataset.RotationPhaseEvidence);
        AddList(root, "advancedLayerAvailability", dataset.AdvancedLayerAvailability);
        AddList(root, "infrastructure", dataset.Infrastructure);
        AddList(root, "provenance", dataset.Provenance);
        AddList(root, "regions", dataset.Regions);
        AddList(root, "volumes", dataset.Volumes);
        AddList(root, "diagnostics", dataset.Diagnostics);
        dataset.Diagnostics.Add("Runtime source: StarSyncUniverse snapshot baseline/refresh; Local Data.p4k access is not required to render this dataset.");
        return dataset;
    }

    private static void AddList<T>(JsonElement root, string propertyName, List<T> target)
    {
        if (!TryGetProperty(root, propertyName, out var property) || property.ValueKind != JsonValueKind.Array) return;
        var values = JsonSerializer.Deserialize<List<T>>(property.GetRawText(), Options);
        if (values is not null) target.AddRange(values);
    }

    private static string RequiredString(JsonElement root, string propertyName) =>
        OptionalString(root, propertyName) ?? throw new InvalidDataException($"Snapshot is missing required property '{propertyName}'.");

    private static string? OptionalString(JsonElement root, string propertyName) =>
        TryGetProperty(root, propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static double? OptionalDouble(JsonElement root, string propertyName) =>
        TryGetProperty(root, propertyName, out var property) && property.ValueKind == JsonValueKind.Number
            ? property.GetDouble()
            : null;

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value)) return true;
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }
}

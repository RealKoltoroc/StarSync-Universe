using System.Text.Json;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class UniverseIndexWriter
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static async Task<string> WriteAsync(
        IReadOnlyDictionary<string, UniverseDataset> datasets,
        IReadOnlyList<JumpConnection> jumpConnections,
        IReadOnlyList<SystemRouteResult> routeProofs,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StarSyncUniverse", "Snapshots");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, "universe.current.json");
        var galaxySystems = GalaxySystemBuilder.Build(datasets, jumpConnections);
        var galaxySchematic = GalaxySchematicBuilder.Build(datasets, jumpConnections);
        var payload = new
        {
            generatedUtc = DateTimeOffset.UtcNow,
            galaxyFrame = new
            {
                frameId = "galaxy:local",
                positionStatus = galaxySystems.All(x => x.GalaxyX.HasValue && x.GalaxyY.HasValue && x.GalaxyZ.HasValue)
                    ? "LOCAL_DIRECT_DATACORE_COORDINATES_UNIT_SEMANTICS_UNRESOLVED"
                    : "PARTIAL_LOCAL_DATACORE_COORDINATES"
            },
            galaxySystems,
            galaxySchematic,
            systems = datasets.Values
                .OrderBy(d => d.System, StringComparer.OrdinalIgnoreCase)
                .Select(d => new
                {
                    d.System,
                    d.Build,
                    entities = d.Entities.Count,
                    canonicalNodes = d.CanonicalNodes.Count,
                    frames = d.Frames.Count,
                    placements = d.Placements.Count,
                    bodies = d.Bodies.Count,
                    temporalTransforms = d.TemporalTransforms.Count,
                    bodyAnchors = d.BodyAnchors.Count,
                    nearSurfaceAnchors = d.BodyAnchors.Count(a => a.AnchorScope == "NEAR_SURFACE"),
                    orientationEvidence = d.OrientationEvidence,
                    surfaceCoverage = d.SurfaceCoverage,
                    rotationPhaseEvidence = d.RotationPhaseEvidence,
                    advancedLayerAvailability = d.AdvancedLayerAvailability,
                    provenance = d.Provenance,
                    regions = d.Regions.Count,
                    volumes = d.Volumes.Count,
                    validation = d.Diagnostics.FirstOrDefault(x => x.StartsWith("Validation:", StringComparison.Ordinal))
                }),
            jumpConnections,
            routeProofs
        };

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, payload, Options, cancellationToken);
        return path;
    }
}

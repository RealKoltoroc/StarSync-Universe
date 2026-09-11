using System.Text.Json;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class UniverseSnapshotWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static async Task<string> WriteAsync(UniverseDataset dataset, CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StarSyncUniverse", "Snapshots");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"{dataset.System}.current.json");
        var payload = new
        {
            generatedUtc = DateTimeOffset.UtcNow,
            dataset.System,
            dataset.Build,
            dataset.PrimarySource,
            dataset.SecondarySource,
            dataset.GalaxyX,
            dataset.GalaxyY,
            dataset.GalaxyZ,
            dataset.GalaxyPositionStatus,
            dataset.GalaxyPositionAuthority,
            entities = dataset.Entities,
            canonicalNodes = dataset.CanonicalNodes,
            frames = dataset.Frames,
            placements = dataset.Placements,
            bodies = dataset.Bodies,
            simulatedOrbits = dataset.SimulatedOrbits,
            temporalTransforms = dataset.TemporalTransforms,
            bodyAnchors = dataset.BodyAnchors,
            orientationEvidence = dataset.OrientationEvidence,
            surfaceCoverage = dataset.SurfaceCoverage,
            rotationPhaseEvidence = dataset.RotationPhaseEvidence,
            advancedLayerAvailability = dataset.AdvancedLayerAvailability,
            infrastructure = dataset.Infrastructure,
            provenance = dataset.Provenance,
            regions = dataset.Regions,
            volumes = dataset.Volumes,
            diagnostics = dataset.Diagnostics
        };

        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, payload, Options, cancellationToken);
        return path;
    }
}

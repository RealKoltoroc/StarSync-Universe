using StarSyncUniverse.Domain;
using StarSyncUniverse.Transforms;

namespace StarSyncUniverse.Renderer;

public static class RenderFrameSnapshotBuilder
{
    public static RenderFrameSnapshot Build(UniverseDataset dataset, Vector3D floatingOrigin, double cullRadiusMeters = double.PositiveInfinity, bool includeHidden = true)
    {
        if (cullRadiusMeters <= 0 || double.IsNaN(cullRadiusMeters)) throw new ArgumentOutOfRangeException(nameof(cullRadiusMeters));
        var points = new List<RenderPointRecord>();
        var culled = 0;

        foreach (var entity in dataset.Entities)
        {
            if (!includeHidden && entity.Hidden)
            {
                culled++;
                continue;
            }

            var relative = new Vector3D(entity.X - floatingOrigin.X, entity.Y - floatingOrigin.Y, entity.Z - floatingOrigin.Z);
            var distance = relative.Length;
            if (distance > cullRadiusMeters)
            {
                culled++;
                continue;
            }

            points.Add(new RenderPointRecord(
                entity.Id,
                entity.Name,
                entity.Type,
                relative.X,
                relative.Y,
                relative.Z,
                distance,
                ClassifyLod(distance),
                entity.Hidden,
                "RENDER_DERIVED_FLOATING_ORIGIN"));
        }

        return new RenderFrameSnapshot(
            dataset.System,
            floatingOrigin.X,
            floatingOrigin.Y,
            floatingOrigin.Z,
            cullRadiusMeters,
            points.OrderBy(x => x.DistanceFromOriginMeters).ToArray(),
            culled,
            "C# DOUBLE_CORE_TO_RELATIVE_RENDER_COORDINATES",
            "RENDER_DERIVED_NO_UNIVERSE_AUTHORITY_CHANGE");
    }

    private static string ClassifyLod(double distanceMeters) => distanceMeters switch
    {
        <= 100_000d => "LOCAL",
        <= 10_000_000d => "BODY",
        <= 1_000_000_000d => "PLANETARY",
        _ => "SYSTEM"
    };
}

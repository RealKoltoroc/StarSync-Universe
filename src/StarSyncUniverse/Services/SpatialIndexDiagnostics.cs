using StarSyncUniverse.Domain;
using StarSyncUniverse.Transforms;

namespace StarSyncUniverse.Services;

public static class SpatialIndexDiagnostics
{
    public static IReadOnlyList<string> Run(UniverseDataset dataset)
    {
        var messages = new List<string>();
        if (dataset.Entities.Count == 0) return messages;

        var index = new SystemSpatialIndex(dataset);
        var anchor = dataset.Entities.FirstOrDefault(e => e.Type.Equals("Planet", StringComparison.OrdinalIgnoreCase))
                     ?? dataset.Entities[0];
        var center = new Vector3D(anchor.X, anchor.Y, anchor.Z);
        const double radius = 150_000_000d;

        var indexed = index.FindWithinRadius(center, radius).Select(e => e.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var brute = dataset.Entities.Where(e =>
        {
            var dx = e.X - center.X;
            var dy = e.Y - center.Y;
            var dz = e.Z - center.Z;
            return dx * dx + dy * dy + dz * dz <= radius * radius;
        }).Select(e => e.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = brute.Except(indexed, StringComparer.OrdinalIgnoreCase).Count();
        var extra = indexed.Except(brute, StringComparer.OrdinalIgnoreCase).Count();
        messages.Add($"Spatial index proof: {dataset.System}, anchor={anchor.Name}, radius=150,000 km, indexed={indexed.Count}, brute={brute.Count}, missing={missing}, extra={extra}.");

        var nearest = index.FindNearest(center, 5, 500_000_000d);
        messages.Add("Spatial index nearest proof: " + string.Join(" | ", nearest.Select(n => $"{n.Entity.Name}:{n.DistanceMeters / 1000d:N1} km")));
        return messages;
    }
}

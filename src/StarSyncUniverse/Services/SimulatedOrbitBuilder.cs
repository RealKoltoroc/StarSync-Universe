using StarSyncUniverse.Domain;
using StarSyncUniverse.Transforms;

namespace StarSyncUniverse.Services;

public static class SimulatedOrbitBuilder
{
    private static readonly HashSet<string> OrbitalCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Planet",
        "Moon",
        "Station",
        "SecurityStation",
        "ShippingHub",
        "CommArray",
        "NavPoint",
        "Manmade"
    };

    public static IReadOnlyList<SimulatedOrbitRecord> Build(UniverseDataset dataset)
    {
        var catalog = UniverseCatalogBuilder.Build(dataset).ToDictionary(x => x.PlacementId, StringComparer.OrdinalIgnoreCase);
        var byPlacement = dataset.Entities.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var bodiesByUuid = dataset.Bodies
            .Where(x => !string.IsNullOrWhiteSpace(x.SourceUuid))
            .GroupBy(x => x.SourceUuid!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var result = new List<SimulatedOrbitRecord>();

        foreach (var child in dataset.Entities)
        {
            if (string.IsNullOrWhiteSpace(child.ParentId) || child.DistanceToParentMeters <= 0)
                continue;
            if (!catalog.TryGetValue(child.Id, out var catalogEntry) || !OrbitalCategories.Contains(catalogEntry.Category))
                continue;
            if (!byPlacement.TryGetValue(child.ParentId, out var parent))
                continue;
            bodiesByUuid.TryGetValue(parent.SourceUuid ?? string.Empty, out var parentBody);

            var center = new Vector3D(parent.X, parent.Y, parent.Z);
            var radiusVector = new Vector3D(child.X - parent.X, child.Y - parent.Y, child.Z - parent.Z);
            var radius = radiusVector.Length;
            if (!double.IsFinite(radius) || radius <= 0)
                continue;

            var u = radiusVector.Normalize();
            var axis = parentBody is null
                ? new Vector3D(0, 0, 1)
                : new Vector3D(parentBody.AxisX, parentBody.AxisY, parentBody.AxisZ).Normalize();
            if (axis.Length <= double.Epsilon)
                axis = new Vector3D(0, 0, 1);

            // A single snapshot does not determine an orbit plane. Choose the circular plane that
            // contains the current radius vector and whose normal deviates as little as possible
            // from the parent's rotation axis. This is a deterministic visual simulation, not a
            // claim about the game's physical orbital dynamics.
            var normalCandidate = axis - u * Vector3D.Dot(axis, u);
            if (normalCandidate.Length < 1e-9)
            {
                var fallback = Math.Abs(u.Z) < 0.9 ? new Vector3D(0, 0, 1) : new Vector3D(0, 1, 0);
                normalCandidate = fallback - u * Vector3D.Dot(fallback, u);
            }

            var normal = normalCandidate.Normalize();
            var v = Vector3D.Cross(normal, u).Normalize();
            if (v.Length <= double.Epsilon)
                continue;

            result.Add(new SimulatedOrbitRecord(
                OrbitId: $"simorbit:{dataset.System}:{child.Id}",
                System: dataset.System,
                ChildPlacementId: child.Id,
                ParentPlacementId: parent.Id,
                ChildName: child.Name,
                ParentName: parent.Name,
                Category: catalogEntry.Category,
                CenterX: center.X,
                CenterY: center.Y,
                CenterZ: center.Z,
                RadiusMeters: radius,
                BasisUx: u.X,
                BasisUy: u.Y,
                BasisUz: u.Z,
                BasisVx: v.X,
                BasisVy: v.Y,
                BasisVz: v.Z,
                NormalX: normal.X,
                NormalY: normal.Y,
                NormalZ: normal.Z,
                GeometryModel: parentBody is null
                    ? "CIRCULAR_CURRENT_RADIUS_SYSTEM_PLANE_FALLBACK"
                    : "CIRCULAR_CURRENT_RADIUS_MINIMUM_INCLINATION_TO_PARENT_AXIS",
                SourceAuthority: parentBody is null
                    ? "LOCAL_DIRECT current child/parent XYZ + LOCAL_DERIVED system-plane visualization fallback"
                    : "LOCAL_DIRECT current child/parent XYZ + LOCAL_DIRECT parent rotation axis + LOCAL_DERIVED visualization",
                DataStatus: "SIMULATED_ORBIT_GEOMETRY_NOT_DYNAMIC_TRUTH"));
        }

        return result;
    }
}

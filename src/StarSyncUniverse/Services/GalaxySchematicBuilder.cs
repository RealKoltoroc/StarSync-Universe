using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class GalaxySchematicBuilder
{
    public static IReadOnlyList<GalaxySchematicSystemRecord> Build(
        IReadOnlyDictionary<string, UniverseDataset> datasets,
        IReadOnlyList<JumpConnection> jumps,
        string preferredAnchorSystem = "pyro")
    {
        if (datasets.Count == 0) return [];
        var anchor = datasets.ContainsKey(preferredAnchorSystem)
            ? preferredAnchorSystem
            : datasets.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).First();

        var positions = new Dictionary<string, (double X, double Y, double Z, string? Via)>(StringComparer.OrdinalIgnoreCase)
        {
            [anchor] = (0, 0, 0, null)
        };
        var queue = new Queue<string>();
        queue.Enqueue(anchor);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var origin = positions[current];
            foreach (var jump in jumps.Where(j => j.Traversable &&
                         (j.SystemA.Equals(current, StringComparison.OrdinalIgnoreCase) || j.SystemB.Equals(current, StringComparison.OrdinalIgnoreCase))))
            {
                var target = jump.SystemA.Equals(current, StringComparison.OrdinalIgnoreCase) ? jump.SystemB : jump.SystemA;
                if (!datasets.ContainsKey(target) || positions.ContainsKey(target)) continue;

                var endpoint = jump.EndpointA?.System.Equals(current, StringComparison.OrdinalIgnoreCase) == true
                    ? jump.EndpointA
                    : jump.EndpointB?.System.Equals(current, StringComparison.OrdinalIgnoreCase) == true
                        ? jump.EndpointB
                        : null;
                if (endpoint is null) continue;

                var length = Math.Sqrt(endpoint.X * endpoint.X + endpoint.Y * endpoint.Y + endpoint.Z * endpoint.Z);
                if (!double.IsFinite(length) || length <= 0) continue;
                var dx = endpoint.X / length;
                var dy = endpoint.Y / length;
                var dz = endpoint.Z / length;
                positions[target] = (origin.X + dx, origin.Y + dy, origin.Z + dz, jump.ConnectionId);
                queue.Enqueue(target);
            }
        }

        return datasets.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Select(system =>
        {
            if (positions.TryGetValue(system, out var p))
                return new GalaxySchematicSystemRecord(
                    system, p.X, p.Y, p.Z, anchor, p.Via,
                    "TOPOLOGY_SCHEMATIC_DIRECTION_FROM_LOCAL_JUMP_ENDPOINT",
                    "UNIT_EDGE_ONLY_NOT_INTERSTELLAR_DISTANCE",
                    "LOCAL_DIRECT jump endpoint direction + LOCAL_DERIVED reciprocal topology",
                    system.Equals(anchor, StringComparison.OrdinalIgnoreCase) ? "SCHEMATIC_ANCHOR" : "LOCAL_DERIVED_SCHEMATIC");

            return new GalaxySchematicSystemRecord(
                system, 0, 0, 0, anchor, null,
                "UNPLACED_SCHEMATIC_NODE",
                "NO_METRIC_DISTANCE",
                "LOCAL_DIRECT system existence",
                "SCHEMATIC_UNRESOLVED");
        }).ToArray();
    }
}

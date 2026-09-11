using StarSyncUniverse.Domain;
using StarSyncUniverse.Transforms;

namespace StarSyncUniverse.Services;

/// <summary>
/// Builds a lightweight visibility graph over CURRENT LIVE QT-capable navigation placements.
/// Physical planet/moon spheres are hard obstacles using LOCAL_DIRECT body radii.
/// No synthetic orbital markers, clearance margins, ship speeds or star radius are invented.
/// </summary>
public sealed class InSystemVisibilityRouter
{
    private readonly UniverseDataset _dataset;
    private readonly Obstacle[] _obstacles;
    private readonly Node[] _navigationCandidates;

    public InSystemVisibilityRouter(UniverseDataset dataset)
    {
        _dataset = dataset;
        _obstacles = BuildObstacles(dataset);
        _navigationCandidates = dataset.Entities
            .Where(IsNavigationCandidate)
            .Select(e => new Node(e.Name, new Vector3D(e.X, e.Y, e.Z)))
            .GroupBy(n => QuantizedKey(n.Position), StringComparer.Ordinal)
            .Select(g => g.First())
            .ToArray();
    }

    public IReadOnlyList<NavigationRouteLeg>? FindRoute(
        Vector3D start,
        Vector3D destination,
        string startLabel,
        string destinationLabel)
    {
        if (!IsBlocked(start, destination))
            return [CreateLeg(start, destination, startLabel, destinationLabel, "IN_SYSTEM_VISIBLE_DIRECT")];

        var candidates = _navigationCandidates
            .Where(n => (n.Position - start).Length > 1d && (n.Position - destination).Length > 1d)
            .ToArray();

        var nodes = new List<Node>(candidates.Length + 2)
        {
            new(startLabel, start),
            new(destinationLabel, destination)
        };
        nodes.AddRange(candidates);

        var n = nodes.Count;
        var distance = Enumerable.Repeat(double.PositiveInfinity, n).ToArray();
        var previous = Enumerable.Repeat(-1, n).ToArray();
        var visited = new bool[n];
        distance[0] = 0d;

        for (var iteration = 0; iteration < n; iteration++)
        {
            var u = -1;
            var best = double.PositiveInfinity;
            for (var i = 0; i < n; i++)
            {
                if (!visited[i] && distance[i] < best)
                {
                    best = distance[i];
                    u = i;
                }
            }
            if (u < 0 || u == 1) break;
            visited[u] = true;

            for (var v = 0; v < n; v++)
            {
                if (v == u || visited[v]) continue;
                var a = nodes[u].Position;
                var b = nodes[v].Position;
                if (IsBlocked(a, b)) continue;
                var edge = (b - a).Length;
                var alt = distance[u] + edge;
                if (alt >= distance[v]) continue;
                distance[v] = alt;
                previous[v] = u;
            }
        }

        if (!double.IsFinite(distance[1])) return null;

        var path = new List<int>();
        for (var at = 1; at >= 0; at = previous[at])
        {
            path.Add(at);
            if (at == 0) break;
            if (previous[at] < 0) return null;
        }
        path.Reverse();

        var legs = new List<NavigationRouteLeg>(Math.Max(1, path.Count - 1));
        for (var i = 0; i < path.Count - 1; i++)
        {
            var a = nodes[path[i]];
            var b = nodes[path[i + 1]];
            legs.Add(CreateLeg(a.Position, b.Position, a.Label, b.Label, "IN_SYSTEM_VISIBILITY_WAYPOINT"));
        }
        return legs;
    }

    public bool IsBlocked(Vector3D from, Vector3D to) => _obstacles.Any(o => SegmentIntersectsSphere(from, to, o));

    private static bool IsNavigationCandidate(UniverseEntity entity)
    {
        if (!entity.QuantumTravelValid) return false;
        if (entity.Type.Equals("Star", StringComparison.OrdinalIgnoreCase) ||
            entity.Type.Equals("Planet", StringComparison.OrdinalIgnoreCase) ||
            entity.Type.Equals("Moon", StringComparison.OrdinalIgnoreCase))
            return false;

        return UniverseCatalogBuilder.Classify(entity) is
            "NavPoint" or "Gateway" or "JumpPoint" or "LagrangePoint" or
            "Station" or "BreakerStation" or "LogisticsStation" or
            "CommArray" or "SecurityStation" or "ShippingHub";
    }

    private static Obstacle[] BuildObstacles(UniverseDataset dataset)
    {
        var result = new List<Obstacle>();
        foreach (var body in dataset.Bodies.Where(b => b.RadiusMeters > 0d))
        {
            UniverseEntity? entity = null;
            if (!string.IsNullOrWhiteSpace(body.SourceUuid))
                entity = dataset.Entities.FirstOrDefault(e => string.Equals(e.SourceUuid, body.SourceUuid, StringComparison.OrdinalIgnoreCase));
            entity ??= dataset.Entities.FirstOrDefault(e => e.Name.Equals(body.Name, StringComparison.OrdinalIgnoreCase));
            if (entity is null) continue;
            result.Add(new Obstacle(body.Name, new Vector3D(entity.X, entity.Y, entity.Z), body.RadiusMeters));
        }
        return result.ToArray();
    }

    private static bool SegmentIntersectsSphere(Vector3D from, Vector3D to, Obstacle obstacle)
    {
        // A route whose endpoint is the body itself is allowed to terminate there; ingress semantics are a later layer.
        if ((from - obstacle.Center).Length < obstacle.RadiusMeters * 0.01 ||
            (to - obstacle.Center).Length < obstacle.RadiusMeters * 0.01)
            return false;

        var d = to - from;
        var len2 = d.X * d.X + d.Y * d.Y + d.Z * d.Z;
        if (len2 <= double.Epsilon) return false;
        var f = from - obstacle.Center;
        var t = -(f.X * d.X + f.Y * d.Y + f.Z * d.Z) / len2;
        t = Math.Clamp(t, 0d, 1d);
        var closest = new Vector3D(from.X + d.X * t, from.Y + d.Y * t, from.Z + d.Z * t);
        return (closest - obstacle.Center).Length < obstacle.RadiusMeters;
    }

    private NavigationRouteLeg CreateLeg(Vector3D from, Vector3D to, string fromLabel, string toLabel, string status) =>
        new(
            LegType: status,
            System: _dataset.System,
            FromLabel: fromLabel,
            ToLabel: toLabel,
            FromX: from.X,
            FromY: from.Y,
            FromZ: from.Z,
            ToX: to.X,
            ToY: to.Y,
            ToZ: to.Z,
            DistanceMeters: (to - from).Length,
            JumpConnectionId: null,
            SourceAuthority: "LOCAL_DERIVED visibility graph from CURRENT LIVE placements + LOCAL_DIRECT planet/moon radii",
            DataStatus: "LOCAL_DERIVED_VISIBILITY_NO_SYNTHETIC_OM_NO_STAR_RADIUS");

    private static string QuantizedKey(Vector3D p) => $"{Math.Round(p.X):R}|{Math.Round(p.Y):R}|{Math.Round(p.Z):R}";

    private sealed record Node(string Label, Vector3D Position);
    private sealed record Obstacle(string Name, Vector3D Center, double RadiusMeters);
}

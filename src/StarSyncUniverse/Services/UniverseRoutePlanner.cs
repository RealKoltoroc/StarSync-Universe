using StarSyncUniverse.Domain;
using StarSyncUniverse.Transforms;

namespace StarSyncUniverse.Services;

public sealed class UniverseRoutePlanner
{
    private readonly IReadOnlyDictionary<string, UniverseDataset> _datasets;
    private readonly IReadOnlyList<JumpConnection> _jumps;
    private readonly IReadOnlyDictionary<string, InSystemVisibilityRouter> _localRouters;
    private readonly IReadOnlyDictionary<string, JumpConnection> _jumpById;
    private readonly Dictionary<string, SystemRouteResult> _systemRouteCache = new(StringComparer.OrdinalIgnoreCase);

    public UniverseRoutePlanner(IReadOnlyDictionary<string, UniverseDataset> datasets, IReadOnlyList<JumpConnection> jumps)
    {
        _datasets = datasets;
        _jumps = jumps;
        _localRouters = datasets.ToDictionary(
            pair => pair.Key,
            pair => new InSystemVisibilityRouter(pair.Value),
            StringComparer.OrdinalIgnoreCase);
        _jumpById = jumps.ToDictionary(x => x.ConnectionId, StringComparer.OrdinalIgnoreCase);
    }

    public NavigationRouteResult FindRoute(NavigationTarget start, NavigationTarget destination)
    {
        var radialBodyRoute = TryCreateBodyRelativeRadialRoute(start, destination);
        if (radialBodyRoute is not null)
            return radialBodyRoute;

        var surfaceGate = ValidateSurfaceRoutingAuthority(start, destination);
        if (surfaceGate is not null)
            return surfaceGate;

        var startPoint = Resolve(start);
        var destinationPoint = Resolve(destination);

        if (start.System.Equals(destination.System, StringComparison.OrdinalIgnoreCase))
        {
            if (!_datasets.TryGetValue(start.System, out var dataset))
                throw new KeyNotFoundException($"Unknown system '{start.System}'.");
            var localRouteLegs = _localRouters[dataset.System].FindRoute(startPoint, destinationPoint, start.Label, destination.Label);
            if (localRouteLegs is null)
                return new NavigationRouteResult(start, destination, false, [], 0d, "LOCAL_ROUTE_BLOCKED_NO_PROVEN_VISIBILITY_PATH");
            var distance = localRouteLegs.Where(x => x.DistanceMeters.HasValue).Sum(x => x.DistanceMeters!.Value);
            var status = localRouteLegs.Count == 1 && localRouteLegs[0].LegType == "IN_SYSTEM_VISIBLE_DIRECT"
                ? "LOCAL_STATIC_VISIBILITY_DIRECT"
                : "LOCAL_STATIC_VISIBILITY_ROUTED_VIA_QT_NODES";
            return new NavigationRouteResult(start, destination, true, localRouteLegs, distance, status);
        }

        var systemRoute = FindSystemRoute(start.System, destination.System);
        if (!systemRoute.Found)
            return new NavigationRouteResult(start, destination, false, [], 0d, systemRoute.DataStatus);

        var legs = new List<NavigationRouteLeg>();
        var currentPoint = startPoint;
        var currentLabel = start.Label;

        for (var i = 0; i < systemRoute.ConnectionIds.Count; i++)
        {
            var connection = _jumpById[systemRoute.ConnectionIds[i]];
            var currentSystem = systemRoute.Systems[i];
            var nextSystem = systemRoute.Systems[i + 1];
            var outbound = EndpointFor(connection, currentSystem, nextSystem)
                ?? throw new InvalidOperationException($"Route connection {connection.ConnectionId} has no outbound endpoint for {currentSystem}->{nextSystem}.");
            var inbound = EndpointFor(connection, nextSystem, currentSystem)
                ?? throw new InvalidOperationException($"Route connection {connection.ConnectionId} has no inbound endpoint for {nextSystem}<-{currentSystem}.");

            var outboundPoint = new Vector3D(outbound.X, outbound.Y, outbound.Z);
            var localLegs = CreateLocalLegs(currentSystem, currentPoint, outboundPoint, currentLabel, outbound.Name);
            if (localLegs is null)
                return new NavigationRouteResult(start, destination, false, legs, legs.Where(x => x.DistanceMeters.HasValue).Sum(x => x.DistanceMeters!.Value), $"LOCAL_ROUTE_BLOCKED_BEFORE_JUMP:{currentSystem}");
            legs.AddRange(localLegs);

            legs.Add(new NavigationRouteLeg(
                LegType: "JUMP",
                System: currentSystem,
                FromLabel: outbound.Name,
                ToLabel: inbound.Name,
                FromX: outbound.X,
                FromY: outbound.Y,
                FromZ: outbound.Z,
                ToX: inbound.X,
                ToY: inbound.Y,
                ToZ: inbound.Z,
                DistanceMeters: null,
                JumpConnectionId: connection.ConnectionId,
                SourceAuthority: "LOCAL_DIRECT reciprocal jump endpoints",
                DataStatus: "LOCAL_TOPOLOGICAL_RECIPROCAL_JUMP"));

            currentPoint = new Vector3D(inbound.X, inbound.Y, inbound.Z);
            currentLabel = inbound.Name;
        }

        var finalLegs = CreateLocalLegs(destination.System, currentPoint, destinationPoint, currentLabel, destination.Label);
        if (finalLegs is null)
            return new NavigationRouteResult(start, destination, false, legs, legs.Where(x => x.DistanceMeters.HasValue).Sum(x => x.DistanceMeters!.Value), $"LOCAL_ROUTE_BLOCKED_AFTER_JUMP:{destination.System}");
        legs.AddRange(finalLegs);

        var measurableDistance = legs.Where(x => x.DistanceMeters.HasValue).Sum(x => x.DistanceMeters!.Value);
        return new NavigationRouteResult(start, destination, true, legs, measurableDistance, "LOCAL_STATIC_VISIBILITY_PLUS_RECIPROCAL_JUMPS");
    }

    private SystemRouteResult FindSystemRoute(string startSystem, string destinationSystem)
    {
        var key = $"{startSystem.Trim().ToLowerInvariant()}|{destinationSystem.Trim().ToLowerInvariant()}";
        if (_systemRouteCache.TryGetValue(key, out var cached))
            return cached;
        var route = InterSystemRoutePlanner.FindRoute(_jumps, startSystem, destinationSystem);
        _systemRouteCache[key] = route;
        return route;
    }

    private IReadOnlyList<NavigationRouteLeg>? CreateLocalLegs(string system, Vector3D from, Vector3D to, string fromLabel, string toLabel)
    {
        if (!_localRouters.TryGetValue(system, out var router))
            throw new KeyNotFoundException($"Unknown system '{system}'.");
        return router.FindRoute(from, to, fromLabel, toLabel);
    }

    private NavigationRouteResult? TryCreateBodyRelativeRadialRoute(NavigationTarget start, NavigationTarget destination)
    {
        if (!start.System.Equals(destination.System, StringComparison.OrdinalIgnoreCase))
            return null;

        var startSurface = IsBodyFixed(start);
        var destinationSurface = IsBodyFixed(destination);
        if (startSurface == destinationSurface)
            return null;

        if (!_datasets.TryGetValue(start.System, out var dataset))
            return null;

        var surfaceTarget = startSurface ? start : destination;
        var placementTarget = startSurface ? destination : start;
        if (string.IsNullOrWhiteSpace(placementTarget.PlacementId))
            return null;

        var placementEntity = dataset.Entities.FirstOrDefault(e => e.Id.Equals(placementTarget.PlacementId, StringComparison.OrdinalIgnoreCase));
        if (placementEntity is null || !TryFindContainingBody(dataset, placementEntity, out var placementBody, out var bodyPlacement))
            return null;
        if (!TryResolveSurfaceAltitude(dataset, surfaceTarget, out var surfaceBody, out var surfaceAltitudeMeters))
            return null;
        if (!surfaceBody.Name.Equals(placementBody.Name, StringComparison.OrdinalIgnoreCase))
            return null;

        var bodyCenter = new Vector3D(bodyPlacement.X, bodyPlacement.Y, bodyPlacement.Z);
        var placementPoint = new Vector3D(placementEntity.X, placementEntity.Y, placementEntity.Z);
        var radial = placementPoint - bodyCenter;
        var radialDistance = radial.Length;
        if (!double.IsFinite(radialDistance) || radialDistance <= 0)
            return null;
        var radialUnit = radial * (1.0 / radialDistance);
        var placementAltitudeMeters = radialDistance - placementBody.RadiusMeters;
        var surfaceRadius = Math.Max(0d, placementBody.RadiusMeters + surfaceAltitudeMeters);
        var radialSurfacePoint = bodyCenter + radialUnit * surfaceRadius;
        var distanceMeters = Math.Abs(placementAltitudeMeters - surfaceAltitudeMeters);

        var from = startSurface ? radialSurfacePoint : placementPoint;
        var to = startSurface ? placementPoint : radialSurfacePoint;
        var leg = new NavigationRouteLeg(
            LegType: "BODY_RELATIVE_RADIAL_ALTITUDE",
            System: start.System,
            FromLabel: start.Label,
            ToLabel: destination.Label,
            FromX: from.X,
            FromY: from.Y,
            FromZ: from.Z,
            ToX: to.X,
            ToY: to.Y,
            ToZ: to.Z,
            DistanceMeters: distanceMeters,
            JumpConnectionId: null,
            SourceAuthority: "LOCAL_DIRECT body center/radius + placement XYZ + LOCAL_DIRECT/DERIVED body-local altitude",
            DataStatus: "BODY_RELATIVE_RADIAL_ALTITUDE_ONLY_ABSOLUTE_SURFACE_PHASE_UNRESOLVED");

        return new NavigationRouteResult(
            start,
            destination,
            true,
            [leg],
            distanceMeters,
            "LOCAL_BODY_RELATIVE_RADIAL_ALTITUDE_ROUTE_PHASE_UNRESOLVED");
    }

    private static bool IsBodyFixed(NavigationTarget target) =>
        target.CoordinateKind.Contains("BODY_FIXED", StringComparison.OrdinalIgnoreCase) ||
        !string.IsNullOrWhiteSpace(target.SurfaceAnchorId);

    private static bool TryResolveSurfaceAltitude(
        UniverseDataset dataset,
        NavigationTarget target,
        out CelestialBodyPhysical body,
        out double altitudeMeters)
    {
        body = null!;
        altitudeMeters = 0d;
        string? bodyName = target.BodyName;
        if (!string.IsNullOrWhiteSpace(target.SurfaceAnchorId))
        {
            var anchor = dataset.BodyAnchors.FirstOrDefault(a => a.AnchorId.Equals(target.SurfaceAnchorId, StringComparison.OrdinalIgnoreCase));
            if (anchor is null) return false;
            bodyName = anchor.BodyName;
            altitudeMeters = anchor.AltitudeMeters;
        }
        if (string.IsNullOrWhiteSpace(bodyName)) return false;
        body = dataset.Bodies.FirstOrDefault(b => b.Name.Equals(bodyName, StringComparison.OrdinalIgnoreCase))!;
        if (body is null) return false;
        if (string.IsNullOrWhiteSpace(target.SurfaceAnchorId) && target.X.HasValue && target.Y.HasValue && target.Z.HasValue)
            altitudeMeters = Math.Sqrt(target.X.Value * target.X.Value + target.Y.Value * target.Y.Value + target.Z.Value * target.Z.Value) - body.RadiusMeters;
        return double.IsFinite(altitudeMeters);
    }

    private static bool TryFindContainingBody(
        UniverseDataset dataset,
        UniverseEntity entity,
        out CelestialBodyPhysical body,
        out UniverseEntity bodyPlacement)
    {
        body = null!;
        bodyPlacement = null!;
        var byId = dataset.Entities.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);
        UniverseEntity? current = entity;
        for (var depth = 0; depth < 16 && current is not null; depth++)
        {
            if (!string.IsNullOrWhiteSpace(current.SourceUuid))
            {
                var directBody = dataset.Bodies.FirstOrDefault(b => b.SourceUuid?.Equals(current.SourceUuid, StringComparison.OrdinalIgnoreCase) == true);
                if (directBody is not null)
                {
                    body = directBody;
                    bodyPlacement = current;
                    return true;
                }
            }
            if (!string.IsNullOrWhiteSpace(current.ParentSourceUuid))
            {
                var parentBody = dataset.Bodies.FirstOrDefault(b => b.SourceUuid?.Equals(current.ParentSourceUuid, StringComparison.OrdinalIgnoreCase) == true);
                if (parentBody is not null)
                {
                    var placement = dataset.Entities.FirstOrDefault(e => e.SourceUuid?.Equals(parentBody.SourceUuid, StringComparison.OrdinalIgnoreCase) == true);
                    if (placement is not null)
                    {
                        body = parentBody;
                        bodyPlacement = placement;
                        return true;
                    }
                }
            }
            current = !string.IsNullOrWhiteSpace(current.ParentId) && byId.TryGetValue(current.ParentId, out var parent) ? parent : null;
        }
        return false;
    }

    private NavigationRouteResult? ValidateSurfaceRoutingAuthority(NavigationTarget start, NavigationTarget destination)
    {
        foreach (var target in new[] { start, destination })
        {
            var bodyFixedTarget = target.CoordinateKind.Contains("BODY_FIXED", StringComparison.OrdinalIgnoreCase) ||
                                  !string.IsNullOrWhiteSpace(target.SurfaceAnchorId);
            if (!bodyFixedTarget)
                continue;

            if (!_datasets.TryGetValue(target.System, out var dataset))
                return new NavigationRouteResult(start, destination, false, [], 0d, $"SURFACE_ROUTE_UNKNOWN_SYSTEM:{target.System}");

            string? bodyName = target.BodyName;
            if (!string.IsNullOrWhiteSpace(target.SurfaceAnchorId))
            {
                var anchor = dataset.BodyAnchors.FirstOrDefault(a => a.AnchorId.Equals(target.SurfaceAnchorId, StringComparison.OrdinalIgnoreCase));
                if (anchor is null)
                    return new NavigationRouteResult(start, destination, false, [], 0d, $"SURFACE_ROUTE_UNKNOWN_ANCHOR:{target.SurfaceAnchorId}");
                bodyName = anchor.BodyName;
            }

            if (string.IsNullOrWhiteSpace(bodyName))
                return new NavigationRouteResult(start, destination, false, [], 0d, "SURFACE_ROUTE_BODY_UNRESOLVED");

            var body = dataset.Bodies.FirstOrDefault(b => b.Name.Equals(bodyName, StringComparison.OrdinalIgnoreCase));
            if (body is null)
                return new NavigationRouteResult(start, destination, false, [], 0d, $"SURFACE_ROUTE_UNKNOWN_BODY:{bodyName}");

            var bodyUuid = body.SourceUuid;
            var provenOms = dataset.Entities.Where(e =>
                    e.QuantumTravelValid &&
                    e.SourceAuthority.Contains("LOCAL_DIRECT", StringComparison.OrdinalIgnoreCase) &&
                    (!string.IsNullOrWhiteSpace(bodyUuid) && e.ParentSourceUuid?.Equals(bodyUuid, StringComparison.OrdinalIgnoreCase) == true) &&
                    (e.Type.Contains("OrbitalMarker", StringComparison.OrdinalIgnoreCase) ||
                     e.EntityClass?.Contains("OrbitalMarker", StringComparison.OrdinalIgnoreCase) == true ||
                     e.Name.StartsWith("OM-", StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            if (provenOms.Length == 0)
                return new NavigationRouteResult(start, destination, false, [], 0d,
                    $"SURFACE_ROUTE_REQUIRES_PROVEN_OM_UNAVAILABLE:{bodyName}");

            return new NavigationRouteResult(start, destination, false, [], 0d,
                $"SURFACE_ROUTE_OM_AVAILABLE_BUT_BODYFIXED_TO_SYSTEM_PHASE_UNRESOLVED:{bodyName}");
        }

        return null;
    }

    private Vector3D Resolve(NavigationTarget target)
    {
        if (!_datasets.TryGetValue(target.System, out var dataset))
            throw new KeyNotFoundException($"Unknown system '{target.System}'.");

        if (!string.IsNullOrWhiteSpace(target.PlacementId))
        {
            var entity = dataset.Entities.FirstOrDefault(e => e.Id.Equals(target.PlacementId, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException($"Unknown placement '{target.PlacementId}' in {target.System}.");
            return new Vector3D(entity.X, entity.Y, entity.Z);
        }

        if (target.X.HasValue && target.Y.HasValue && target.Z.HasValue)
            return new Vector3D(target.X.Value, target.Y.Value, target.Z.Value);

        throw new ArgumentException("Navigation target requires either PlacementId or XYZ.");
    }

    private static JumpEndpoint? EndpointFor(JumpConnection connection, string system, string destination) =>
        new[] { connection.EndpointA, connection.EndpointB }
            .Where(x => x is not null)
            .Select(x => x!)
            .FirstOrDefault(x => x.System.Equals(system, StringComparison.OrdinalIgnoreCase) &&
                                 x.DestinationSystem.Equals(destination, StringComparison.OrdinalIgnoreCase));
}

using StarSyncUniverse.Domain;
using StarSyncUniverse.Transforms;

namespace StarSyncUniverse.Services;

public sealed class UniverseService : IUniverseService
{
    private readonly IReadOnlyDictionary<string, UniverseDataset> _datasets;
    private readonly IReadOnlyList<JumpConnection> _jumps;
    private readonly Dictionary<string, SystemSpatialIndex> _indices;
    private readonly Dictionary<string, Dictionary<string, UniverseEntity>> _byPlacement;
    private readonly Dictionary<string, IReadOnlyList<UniverseCatalogEntry>> _catalogs;
    private readonly Dictionary<string, IReadOnlyList<SurfaceTargetCatalogEntry>> _surfaceTargets;
    private readonly Dictionary<string, SystemVolumeIndex> _volumeIndices;
    private readonly UniverseRoutePlanner _routePlanner;

    public UniverseService(
        IReadOnlyDictionary<string, UniverseDataset> datasets,
        IReadOnlyList<JumpConnection> jumps)
    {
        _datasets = datasets;
        _jumps = jumps;
        _indices = datasets.ToDictionary(
            pair => pair.Key,
            pair => new SystemSpatialIndex(pair.Value),
            StringComparer.OrdinalIgnoreCase);
        _byPlacement = datasets.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Entities.ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        _catalogs = datasets.ToDictionary(
            pair => pair.Key,
            pair => UniverseCatalogBuilder.Build(pair.Value),
            StringComparer.OrdinalIgnoreCase);
        _surfaceTargets = datasets.ToDictionary(
            pair => pair.Key,
            pair => SurfaceTargetCatalogBuilder.Build(pair.Value),
            StringComparer.OrdinalIgnoreCase);
        _volumeIndices = datasets.ToDictionary(
            pair => pair.Key,
            pair => new SystemVolumeIndex(pair.Value),
            StringComparer.OrdinalIgnoreCase);
        _routePlanner = new UniverseRoutePlanner(datasets, jumps);
        Systems = datasets.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyCollection<string> Systems { get; }

    public UniverseDataset GetSystem(string system) =>
        _datasets.TryGetValue(system, out var dataset)
            ? dataset
            : throw new KeyNotFoundException($"Unknown system '{system}'.");

    public IReadOnlyList<UniverseEntity> GetChildren(string system, string placementId) =>
        GetIndex(system).ChildrenOf(placementId);

    public IReadOnlyList<UniverseEntity> FindBySourceUuid(string system, string sourceUuid) =>
        GetIndex(system).BySourceUuid(sourceUuid);

    public IReadOnlyList<UniverseEntity> FindWithinRadius(string system, Vector3D center, double radiusMeters, bool includeHidden = true) =>
        GetIndex(system).FindWithinRadius(center, radiusMeters, includeHidden);

    public IReadOnlyList<(UniverseEntity Entity, double DistanceMeters)> FindNearest(
        string system,
        Vector3D center,
        int maxCount,
        double maxRadiusMeters = double.PositiveInfinity,
        bool includeHidden = true) =>
        GetIndex(system).FindNearest(center, maxCount, maxRadiusMeters, includeHidden);

    public SpatialPositionSnapshot GetPosition(string system, string placementId)
    {
        var map = GetPlacementMap(system);
        if (!map.TryGetValue(placementId, out var entity))
            throw new KeyNotFoundException($"Unknown placement '{placementId}' in {system}.");

        return new SpatialPositionSnapshot(
            system,
            entity.Id,
            entity.Name,
            entity.X,
            entity.Y,
            entity.Z,
            "SYSTEM_AXES_METERS",
            entity.SourceAuthority,
            entity.DataStatus);
    }

    public double GetDistanceMeters(string system, string placementIdA, string placementIdB) =>
        Measure(system, placementIdA, placementIdB).DistanceMeters;

    public SpatialMeasurement Measure(string system, string placementIdA, string placementIdB)
    {
        var map = GetPlacementMap(system);
        if (!map.TryGetValue(placementIdA, out var a))
            throw new KeyNotFoundException($"Unknown placement '{placementIdA}' in {system}.");
        if (!map.TryGetValue(placementIdB, out var b))
            throw new KeyNotFoundException($"Unknown placement '{placementIdB}' in {system}.");

        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var dz = b.Z - a.Z;
        var horizontal = Math.Sqrt(dx * dx + dy * dy);
        var distance = Math.Sqrt(horizontal * horizontal + dz * dz);
        var azimuth = Math.Atan2(dy, dx) * 180d / Math.PI;
        if (azimuth < 0) azimuth += 360d;
        var elevationAngle = Math.Atan2(dz, horizontal) * 180d / Math.PI;

        return new SpatialMeasurement(
            system, a.Id, b.Id, a.Name, b.Name,
            a.X, a.Y, a.Z, b.X, b.Y, b.Z,
            dx, dy, dz, distance, horizontal, dz,
            azimuth, elevationAngle,
            "SYSTEM_AXES_METERS",
            "LOCAL_DERIVED_FROM_CURRENT_PLACEMENTS");
    }

    public IReadOnlyList<BodyLocalAnchorRecord> GetBodyAnchors(string system, string bodyName, string? anchorScope = null)
    {
        var query = GetSystem(system).BodyAnchors.Where(a => a.BodyName.Equals(bodyName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(anchorScope))
            query = query.Where(a => a.AnchorScope.Equals(anchorScope, StringComparison.OrdinalIgnoreCase));
        return query.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public IReadOnlyList<SurfaceTargetCatalogEntry> GetSurfaceTargets(string system, string? bodyName = null, string? category = null)
    {
        if (!_surfaceTargets.TryGetValue(system, out var targets))
            throw new KeyNotFoundException($"Unknown system '{system}'.");
        var query = targets.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(bodyName))
            query = query.Where(x => x.BodyName.Equals(bodyName, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        return query.ToArray();
    }

    public IReadOnlyList<SpatialRegion> GetRegions(string system) =>
        GetSystem(system).Regions.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlyList<SpatialVolume> GetVolumes(string system) =>
        GetSystem(system).Volumes.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlyList<SpatialVolume> FindVolumesContaining(string system, Vector3D point) =>
        GetVolumeIndex(system).FindContaining(point);

    public IReadOnlyList<UniverseCatalogEntry> SearchCatalog(
        string system,
        string? query = null,
        string? category = null,
        bool includeHidden = false,
        int maxResults = 500)
    {
        if (!_catalogs.TryGetValue(system, out var cachedCatalog))
            throw new KeyNotFoundException($"Unknown system '{system}'.");
        var items = cachedCatalog.AsEnumerable();
        if (!includeHidden)
            items = items.Where(x => !x.Hidden);
        if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
            items = items.Where(x => x.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            items = items.Where(x =>
                x.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Category.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                x.Type.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (x.EntityClass?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.SourceUuid?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.SourcePath?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        return items.Take(Math.Clamp(maxResults, 1, 5000)).ToArray();
    }

    public Vector3D ProjectBodyFixedRelative(string system, string bodyName, Vector3D bodyFixedPoint, TimeSpan elapsedFromReference)
    {
        var (body, entity) = ResolveBody(system, bodyName);
        var model = BodyRotationEngine.FromPhysical(body, entity.Id);
        return BodyFrameTransformEngine.BodyFixedToSystemRelative(new Vector3D(entity.X, entity.Y, entity.Z), bodyFixedPoint, model, elapsedFromReference);
    }

    public Vector3D ProjectSystemToBodyFixedRelative(string system, string bodyName, Vector3D systemPoint, TimeSpan elapsedFromReference)
    {
        var (body, entity) = ResolveBody(system, bodyName);
        var model = BodyRotationEngine.FromPhysical(body, entity.Id);
        return BodyFrameTransformEngine.SystemToBodyFixedRelative(new Vector3D(entity.X, entity.Y, entity.Z), systemPoint, model, elapsedFromReference);
    }

    public SystemRouteResult FindSystemRoute(string startSystem, string destinationSystem) =>
        InterSystemRoutePlanner.FindRoute(_jumps, startSystem, destinationSystem);

    public NavigationRouteResult FindRoute(NavigationTarget start, NavigationTarget destination) =>
        _routePlanner.FindRoute(start, destination);

    private (CelestialBodyPhysical Body, UniverseEntity Entity) ResolveBody(string system, string bodyName)
    {
        var dataset = GetSystem(system);
        var body = dataset.Bodies.FirstOrDefault(b => b.Name.Equals(bodyName, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Unknown body '{bodyName}' in {system}.");
        var entity = dataset.Entities.FirstOrDefault(e =>
            (!string.IsNullOrWhiteSpace(body.SourceUuid) && string.Equals(e.SourceUuid, body.SourceUuid, StringComparison.OrdinalIgnoreCase)) ||
            e.Name.Equals(body.Name, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"No spatial placement found for body '{bodyName}' in {system}.");
        return (body, entity);
    }

    private SystemSpatialIndex GetIndex(string system) =>
        _indices.TryGetValue(system, out var index)
            ? index
            : throw new KeyNotFoundException($"Unknown system '{system}'.");

    private Dictionary<string, UniverseEntity> GetPlacementMap(string system) =>
        _byPlacement.TryGetValue(system, out var map)
            ? map
            : throw new KeyNotFoundException($"Unknown system '{system}'.");

    private SystemVolumeIndex GetVolumeIndex(string system) =>
        _volumeIndices.TryGetValue(system, out var index)
            ? index
            : throw new KeyNotFoundException($"Unknown system '{system}'.");
}

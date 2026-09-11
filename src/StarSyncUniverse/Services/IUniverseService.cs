using StarSyncUniverse.Domain;
using StarSyncUniverse.Transforms;

namespace StarSyncUniverse.Services;

public interface IUniverseService
{
    IReadOnlyCollection<string> Systems { get; }
    UniverseDataset GetSystem(string system);
    IReadOnlyList<UniverseEntity> GetChildren(string system, string placementId);
    IReadOnlyList<UniverseEntity> FindBySourceUuid(string system, string sourceUuid);
    IReadOnlyList<UniverseEntity> FindWithinRadius(string system, Vector3D center, double radiusMeters, bool includeHidden = true);
    IReadOnlyList<(UniverseEntity Entity, double DistanceMeters)> FindNearest(string system, Vector3D center, int maxCount, double maxRadiusMeters = double.PositiveInfinity, bool includeHidden = true);
    SpatialPositionSnapshot GetPosition(string system, string placementId);
    double GetDistanceMeters(string system, string placementIdA, string placementIdB);
    SpatialMeasurement Measure(string system, string placementIdA, string placementIdB);
    IReadOnlyList<BodyLocalAnchorRecord> GetBodyAnchors(string system, string bodyName, string? anchorScope = null);
    IReadOnlyList<SurfaceTargetCatalogEntry> GetSurfaceTargets(string system, string? bodyName = null, string? category = null);
    IReadOnlyList<SpatialRegion> GetRegions(string system);
    IReadOnlyList<SpatialVolume> GetVolumes(string system);
    IReadOnlyList<SpatialVolume> FindVolumesContaining(string system, Vector3D point);
    IReadOnlyList<UniverseCatalogEntry> SearchCatalog(string system, string? query = null, string? category = null, bool includeHidden = false, int maxResults = 500);
    Vector3D ProjectBodyFixedRelative(string system, string bodyName, Vector3D bodyFixedPoint, TimeSpan elapsedFromReference);
    Vector3D ProjectSystemToBodyFixedRelative(string system, string bodyName, Vector3D systemPoint, TimeSpan elapsedFromReference);
    SystemRouteResult FindSystemRoute(string startSystem, string destinationSystem);
    NavigationRouteResult FindRoute(NavigationTarget start, NavigationTarget destination);
}

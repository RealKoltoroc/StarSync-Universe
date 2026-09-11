using System.Text.Json;

namespace StarSyncUniverse.Contracts;

public static class UniverseCatalogContract
{
    public const string ApiVersion = "1.2";
}

public sealed record UniverseCatalogCapabilities(
    string ApiVersion,
    IReadOnlyList<string> Systems,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> EnrichmentProviders,
    bool SupportsSpatialCoordinates,
    bool SupportsSourceUuidLookup,
    bool SupportsEnrichment,
    bool SupportsSpatialMeasurements,
    bool SupportsSpatialVolumes);

public sealed record UniverseCatalogQuery(
    string? Text = null,
    IReadOnlyList<string>? Systems = null,
    IReadOnlyList<string>? Categories = null,
    bool IncludeHidden = false,
    int MaxResults = 200);

public sealed record UniverseCatalogObject(
    string System,
    string PlacementId,
    string? SourceUuid,
    string Name,
    string Category,
    string Type,
    string? EntityClass,
    string? ParentPlacementId,
    string? ParentSourceUuid,
    double X,
    double Y,
    double Z,
    bool Hidden,
    bool QuantumTravelValid,
    string? SourcePath,
    string SourceAuthority,
    string DataStatus,
    IReadOnlyList<UniverseEnrichmentDocument> Enrichments);

public sealed record UniverseEnrichmentDocument(
    string Provider,
    string SchemaVersion,
    string? SourceBuild,
    string DataStatus,
    JsonElement Data);

public sealed record UniverseSpatialPosition(
    string System,
    string PlacementId,
    string Name,
    double X,
    double Y,
    double Z,
    string CoordinateFrame,
    string SourceAuthority,
    string DataStatus);

public sealed record UniverseSpatialMeasurement(
    string System,
    string FromPlacementId,
    string ToPlacementId,
    string FromName,
    string ToName,
    double DeltaX,
    double DeltaY,
    double DeltaZ,
    double DistanceMeters,
    double HorizontalDistanceMeters,
    double ElevationDeltaMeters,
    double AzimuthDegreesSystemXY,
    double ElevationAngleDegrees,
    string CoordinateFrame,
    string DataStatus);

public sealed record UniverseSpatialVolumeHit(
    string Id,
    string Name,
    string Type,
    string System,
    double CenterX,
    double CenterY,
    double CenterZ,
    double BoundingRadiusMeters,
    string SourceAuthority,
    string DataStatus);

public interface IUniverseCatalogApi
{
    Task<UniverseCatalogCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UniverseCatalogObject>> SearchAsync(UniverseCatalogQuery query, CancellationToken cancellationToken = default);
    Task<UniverseCatalogObject?> GetByPlacementIdAsync(string system, string placementId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UniverseCatalogObject>> GetBySourceUuidAsync(string system, string sourceUuid, CancellationToken cancellationToken = default);
    Task<UniverseSpatialPosition> GetPositionAsync(string system, string placementId, CancellationToken cancellationToken = default);
    Task<UniverseSpatialMeasurement> MeasureAsync(string system, string fromPlacementId, string toPlacementId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UniverseSpatialVolumeHit>> FindVolumesContainingAsync(string system, double x, double y, double z, CancellationToken cancellationToken = default);
}

public interface IUniverseCatalogEnrichmentProvider
{
    string ProviderId { get; }
    string SchemaVersion { get; }
    Task<UniverseEnrichmentDocument?> EnrichAsync(UniverseCatalogObject coreObject, CancellationToken cancellationToken = default);
}

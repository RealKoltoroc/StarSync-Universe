namespace StarSyncUniverse.Domain;

public enum MapVisualClass
{
    Unknown,
    Station,
    RestStop,
    OrbitalPlatform,
    City,
    LandingZone,
    Outpost,
    MiningBase,
    ResearchBase,
    IndustrialFacility,
    Bunker,
    Cave,
    Settlement,
    CommArray,
    Relay,
    JumpPoint,
    RacingTrack,
    SurfacePoi,
    MissionLocation,
    CelestialBody,
    SpatialRegion
}

public enum MapVisualRepresentation
{
    Icon2D,
    StandardModel3D,
    OverrideModel3D
}

public sealed record MapVisualAssetDescriptor(
    string AssetKey,
    MapVisualClass VisualClass,
    MapVisualRepresentation Representation,
    string AssetPath,
    bool Exists,
    bool IsOverride,
    string SourceAuthority,
    string DataStatus);

public sealed record SurfaceMapPoint(
    double X01,
    double Y01,
    double LatitudeDegrees,
    double LongitudeDegrees,
    string CoordinateConvention,
    string DataStatus);

public sealed record SurfaceMapTerminatorPoint(
    double X01,
    double Y01,
    double LatitudeDegrees,
    double LongitudeDegrees);

public sealed record SurfaceTextureDescriptor(
    string BodyCanonicalId,
    string BodyName,
    string Projection,
    string AssetPath,
    bool Exists,
    int? PixelWidth,
    int? PixelHeight,
    string CoordinateConvention,
    string SourceAuthority,
    string DataStatus);

namespace StarSyncUniverse.Domain;

public sealed record RenderPointRecord(
    string PlacementId,
    string Name,
    string Type,
    double RelativeX,
    double RelativeY,
    double RelativeZ,
    double DistanceFromOriginMeters,
    string LodBand,
    bool Hidden,
    string DataStatus);

public sealed record RenderFrameSnapshot(
    string System,
    double OriginX,
    double OriginY,
    double OriginZ,
    double CullRadiusMeters,
    IReadOnlyList<RenderPointRecord> Points,
    int CulledCount,
    string CoordinatePrecision,
    string DataStatus);

namespace StarSyncUniverse.Domain;

public sealed record GalaxySchematicSystemRecord(
    string System,
    double DisplayX,
    double DisplayY,
    double DisplayZ,
    string AnchorSystem,
    string? DerivedFromJumpConnectionId,
    string CoordinateMeaning,
    string DistanceMeaning,
    string SourceAuthority,
    string DataStatus);

namespace StarSyncUniverse.Domain;

public sealed record UniverseCatalogEntry(
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
    string DataStatus);

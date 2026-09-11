namespace StarSyncUniverse.Domain;

public sealed record UniverseEntity(
    string Id,
    string? SourceUuid,
    string Name,
    string Type,
    string System,
    string? ParentId,
    string? ParentSourceUuid,
    string? SourcePath,
    string? EntityClass,
    bool Hidden,
    bool QuantumTravelValid,
    double X,
    double Y,
    double Z,
    double Q0,
    double Q1,
    double Q2,
    double Q3,
    double DistanceToParentMeters,
    string SourceAuthority,
    string DataStatus);

public sealed record SpatialRegion(
    string Id,
    string Name,
    string Type,
    string System,
    string ParentFrame,
    double CenterX,
    double CenterY,
    double CenterZ,
    double InnerRadiusMeters,
    double OuterRadiusMeters,
    double ThicknessMeters,
    double DensityScale,
    string? Composition,
    string SourcePath,
    string SourceAuthority,
    string DataStatus);

public sealed record CelestialBodyPhysical(
    string SourcePath,
    string ContainerName,
    string? SourceUuid,
    string Name,
    double RadiusMeters,
    double RotationPeriodHoursRaw,
    double RotationPeriodSeconds,
    double AxisX,
    double AxisY,
    double AxisZ,
    string SourceAuthority,
    string DataStatus);

public sealed record CanonicalSpatialNode(
    string CanonicalId,
    string? SourceUuid,
    string Name,
    string Type,
    string System,
    string? ParentSourceUuid,
    string SourceAuthority,
    string DataStatus);

public sealed record SpatialFrameRecord(
    string FrameId,
    string? ParentFrameId,
    string FrameType,
    string? CanonicalId,
    string? PlacementId,
    string SourceAuthority,
    string DataStatus);

public sealed record SpatialPlacementRecord(
    string PlacementId,
    string CanonicalId,
    string? ParentPlacementId,
    string ParentFrameId,
    double LocalX,
    double LocalY,
    double LocalZ,
    double WorldX,
    double WorldY,
    double WorldZ,
    double Q0,
    double Q1,
    double Q2,
    double Q3,
    string PlacementMode,
    string TranslationSpace,
    string? SourcePath,
    string SourceAuthority,
    string DataStatus);

public sealed record SpatialVolume(
    string Id,
    string Name,
    string Type,
    string System,
    string ParentFrameId,
    string PlacementId,
    double CenterX,
    double CenterY,
    double CenterZ,
    double MinLocalX,
    double MinLocalY,
    double MinLocalZ,
    double MaxLocalX,
    double MaxLocalY,
    double MaxLocalZ,
    double BoundingRadiusMeters,
    string? TemplateSourcePath,
    string SourceAuthority,
    string DataStatus);

public sealed record JumpEndpoint(
    string EndpointId,
    string System,
    string DestinationSystem,
    string Name,
    string EndpointType,
    string? SourceUuid,
    string PlacementId,
    double X,
    double Y,
    double Z,
    string SourceAuthority,
    string DataStatus);

public sealed record JumpConnection(
    string ConnectionId,
    string SystemA,
    string SystemB,
    JumpEndpoint? EndpointA,
    JumpEndpoint? EndpointB,
    bool ReciprocalEndpointsPresent,
    string ConnectionType,
    string Availability,
    bool Traversable,
    string SourceAuthority,
    string DataStatus);

public sealed record SystemRouteResult(
    string StartSystem,
    string DestinationSystem,
    bool Found,
    IReadOnlyList<string> Systems,
    IReadOnlyList<string> ConnectionIds,
    string DataStatus);

public sealed record SpatialPositionSnapshot(
    string System,
    string PlacementId,
    string Name,
    double X,
    double Y,
    double Z,
    string CoordinateFrame,
    string SourceAuthority,
    string DataStatus);

public sealed record SpatialMeasurement(
    string System,
    string FromPlacementId,
    string ToPlacementId,
    string FromName,
    string ToName,
    double FromX,
    double FromY,
    double FromZ,
    double ToX,
    double ToY,
    double ToZ,
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

public sealed record NavigationTarget(
    string System,
    string Label,
    string? PlacementId,
    double? X,
    double? Y,
    double? Z,
    string CoordinateKind,
    string DataStatus,
    string? SurfaceAnchorId = null,
    string? BodyName = null);

public sealed record NavigationRouteLeg(
    string LegType,
    string System,
    string FromLabel,
    string ToLabel,
    double FromX,
    double FromY,
    double FromZ,
    double ToX,
    double ToY,
    double ToZ,
    double? DistanceMeters,
    string? JumpConnectionId,
    string SourceAuthority,
    string DataStatus);

public sealed record NavigationRouteResult(
    NavigationTarget Start,
    NavigationTarget Destination,
    bool Found,
    IReadOnlyList<NavigationRouteLeg> Legs,
    double MeasurableInSystemDistanceMeters,
    string DataStatus);

public sealed record GalaxySystemRecord(
    string System,
    string FrameId,
    double? GalaxyX,
    double? GalaxyY,
    double? GalaxyZ,
    string PositionStatus,
    int ReciprocalJumpDegree,
    string SourceAuthority,
    string DataStatus);

public sealed record SourceProvenanceRecord(
    string SourceId,
    string SourceType,
    string Path,
    bool IsPrimary,
    long? FileLength,
    DateTimeOffset? LastWriteUtc,
    string Fingerprint,
    string FingerprintKind,
    string DataStatus);

public sealed record BookmarkRecord(
    Guid Id,
    string Name,
    string System,
    string ReferenceFrameId,
    string CoordinateKind,
    double X,
    double Y,
    double Z,
    string? BodySourceUuid,
    double? LatitudeDegrees,
    double? LongitudeDegrees,
    double? AltitudeMeters,
    IReadOnlyList<string> Tags,
    string Visibility,
    long Revision,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    string Category = "Infos",
    string Note = "",
    string Color = "#f59e0b",
    bool ShowOnMap = true,
    string BookmarkScope = "SPACE",
    string CreatorClientId = "",
    string ContentFingerprint = "");

public sealed record TemporalTransformRecord(
    string TransformId,
    string System,
    string? SourceUuid,
    string Name,
    string TransformType,
    string FromFrameId,
    string ToFrameId,
    double AxisX,
    double AxisY,
    double AxisZ,
    double PeriodSeconds,
    DateTimeOffset? AlignmentUtc,
    double? AlignmentAngleRadians,
    string SourceAuthority,
    string DataStatus);

public sealed record BodyLocalAnchorRecord(
    string AnchorId,
    string System,
    string BodyName,
    string? BodySourceUuid,
    string BodyFixedFrameId,
    string Name,
    string? SourceUuid,
    string? EntityClass,
    double BodyLocalX,
    double BodyLocalY,
    double BodyLocalZ,
    double ReferenceRadiusMeters,
    double AxisLatitudeDegrees,
    double AxisLongitudeDegrees,
    double AltitudeMeters,
    string AnchorScope,
    string CoordinateConvention,
    string SourcePath,
    string SourceAuthority,
    string DataStatus);

public sealed record SurfaceTargetCatalogEntry(
    string AnchorId,
    string System,
    string BodyName,
    string Name,
    string Category,
    string AnchorScope,
    string? SourceUuid,
    string? EntityClass,
    double BodyLocalX,
    double BodyLocalY,
    double BodyLocalZ,
    double ReferenceRadiusMeters,
    double AxisLatitudeDegrees,
    double AxisLongitudeDegrees,
    double AltitudeMeters,
    string CoordinateConvention,
    string SourcePath,
    string SourceAuthority,
    string DataStatus,
    bool HiddenOnMap);

public sealed record OrientationConventionEvidence(
    string System,
    string ComponentOrder,
    int PlacementCount,
    int IdentityWxyzCount,
    int NonTrivialRotationCount,
    double MaxQuaternionNormError,
    string HandednessStatus,
    string ActivePassiveStatus,
    string TranslationComposition,
    string SourceAuthority,
    string DataStatus);

public sealed record SurfaceCoverageSummaryRecord(
    string System,
    string BodyName,
    string? BodySourceUuid,
    int AnchorCount,
    int NearSurfaceCount,
    int ElevatedAtmosphericCount,
    int OrbitalCount,
    int SubsurfaceCount,
    double? MinimumAltitudeMeters,
    double? MaximumAltitudeMeters,
    string CoordinateConvention,
    string DataStatus);

public sealed record RotationPhaseEvidenceRecord(
    string System,
    int ContainersScanned,
    bool PlanetRotationSpeedPresent,
    bool PlanetAxisPresent,
    IReadOnlyList<string> AbsolutePhaseCandidateFields,
    string SourceAuthority,
    string DataStatus);

public sealed record ObjectContainerGraphNode(
    string NodeId,
    string? ParentNodeId,
    int Depth,
    string ContainerSourcePath,
    string? ReferencedContainerSourcePath,
    string? SourceUuid,
    string? EntityClass,
    string? Name,
    double LocalX,
    double LocalY,
    double LocalZ,
    double Q0,
    double Q1,
    double Q2,
    double Q3,
    string TranslationStatus,
    string SourceAuthority,
    string DataStatus);

public sealed record ObjectContainerGraphResult(
    string RootSourcePath,
    int ContainersResolved,
    IReadOnlyList<ObjectContainerGraphNode> Nodes,
    IReadOnlyList<string> UnresolvedReferences,
    string DataStatus);

public sealed record AdvancedLayerAvailabilityRecord(
    string Layer,
    string CurrentBuild,
    string? CandidateSource,
    int? CandidateSourceP4,
    bool SameBuildVerified,
    bool AuthoritativeImportEnabled,
    string SourceAuthority,
    string DataStatus,
    string Reason);

/// <summary>
/// Build-specific infrastructure evidence attached to one canonical placement. Infrastructure is
/// deliberately kept outside UniverseEntity so future facilities (cargo, repair, medical, etc.) can
/// be added without widening the core spatial entity contract. Presence is authoritative only when
/// proven from the CURRENT LIVE object-container hierarchy; SCUnpacked is secondary corroboration.
/// </summary>
public sealed record LocationInfrastructureRecord(
    string PlacementId,
    string System,
    string InfrastructureType,
    bool IsPresent,
    int Count,
    bool ScUnpackedCorroborated,
    string? ScUnpackedEvidence,
    string SourcePath,
    string EvidencePath,
    string SourceAuthority,
    string DataStatus);

public sealed class UniverseDataset
{
    public required string System { get; init; }
    public required string Build { get; init; }
    public required string PrimarySource { get; init; }
    public string? SecondarySource { get; init; }
    public double? GalaxyX { get; set; }
    public double? GalaxyY { get; set; }
    public double? GalaxyZ { get; set; }
    public string GalaxyPositionStatus { get; set; } = "MISSING_LOCAL_METRIC_GALAXY_POSITION";
    public string GalaxyPositionAuthority { get; set; } = "UNRESOLVED";
    public List<UniverseEntity> Entities { get; } = [];
    public List<CanonicalSpatialNode> CanonicalNodes { get; } = [];
    public List<SpatialFrameRecord> Frames { get; } = [];
    public List<SpatialPlacementRecord> Placements { get; } = [];
    public List<SpatialRegion> Regions { get; } = [];
    public List<SpatialVolume> Volumes { get; } = [];
    public List<CelestialBodyPhysical> Bodies { get; } = [];
    public List<SimulatedOrbitRecord> SimulatedOrbits { get; } = [];
    public List<TemporalTransformRecord> TemporalTransforms { get; } = [];
    public List<BodyLocalAnchorRecord> BodyAnchors { get; } = [];
    public List<OrientationConventionEvidence> OrientationEvidence { get; } = [];
    public List<SurfaceCoverageSummaryRecord> SurfaceCoverage { get; } = [];
    public List<RotationPhaseEvidenceRecord> RotationPhaseEvidence { get; } = [];
    public List<AdvancedLayerAvailabilityRecord> AdvancedLayerAvailability { get; } = [];
    public List<LocationInfrastructureRecord> Infrastructure { get; } = [];
    public List<SourceProvenanceRecord> Provenance { get; } = [];
    public List<string> Diagnostics { get; } = [];
}

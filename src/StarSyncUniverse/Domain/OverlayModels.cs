namespace StarSyncUniverse.Domain;

public sealed record SavedRouteRecord(
    Guid Id,
    string Name,
    NavigationTarget Start,
    NavigationTarget Destination,
    string RoutingProfile,
    IReadOnlyList<string> Tags,
    string Visibility,
    long Revision,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record UniverseNoteRecord(
    Guid Id,
    string TargetReference,
    string Text,
    string Visibility,
    long Revision,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record UniverseMediaReferenceRecord(
    Guid Id,
    string TargetReference,
    string MediaKey,
    string? LocalPath,
    string? ContentHash,
    string Visibility,
    long Revision,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record OverlayTombstoneRecord(
    Guid Id,
    string EntityKind,
    long Revision,
    DateTimeOffset DeletedUtc);

public sealed record UniverseOverlayEnvelope(
    string SchemaVersion,
    string? SourceBuild,
    long Revision,
    IReadOnlyList<BookmarkRecord> Bookmarks,
    IReadOnlyList<SavedRouteRecord> Routes,
    IReadOnlyList<UniverseNoteRecord> Notes,
    IReadOnlyList<UniverseMediaReferenceRecord> MediaReferences,
    IReadOnlyList<OverlayTombstoneRecord> Tombstones,
    DateTimeOffset GeneratedUtc);

public sealed record OverlayConflictRecord(
    string EntityKind,
    Guid Id,
    long LocalRevision,
    long RemoteRevision,
    string Winner,
    string Reason);

public sealed record OverlayMergeResult(
    UniverseOverlayEnvelope Merged,
    int LocalWinners,
    int RemoteWinners,
    int EqualRecords,
    IReadOnlyList<OverlayConflictRecord> Conflicts,
    string DataStatus);

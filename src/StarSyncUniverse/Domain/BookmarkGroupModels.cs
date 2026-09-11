namespace StarSyncUniverse.Domain;

public sealed record BookmarkGroupRecord(
    Guid Id,
    string Name,
    string Description,
    IReadOnlyList<Guid> BookmarkIds,
    string CreatorClientId,
    long Revision,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    string ContentFingerprint,
    string ShareStatus = "LOCAL");

public sealed record BookmarkGroupPackage(
    string Schema,
    BookmarkGroupRecord Group,
    IReadOnlyList<BookmarkRecord> Bookmarks,
    string PublisherClientId,
    string ContentFingerprint,
    DateTimeOffset ExportedUtc,
    string SignatureAlgorithm,
    string SignerPublicKeySpkiBase64,
    string Signature);

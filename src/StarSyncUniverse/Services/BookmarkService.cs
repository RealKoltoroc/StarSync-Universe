using StarSyncUniverse.Domain;
using StarSyncUniverse.Transforms;

namespace StarSyncUniverse.Services;

public interface IBookmarkService
{
    Task<IReadOnlyList<BookmarkRecord>> ListAsync(CancellationToken cancellationToken = default);
    Task<BookmarkRecord> CreateSystemPointAsync(string name, string system, Vector3D point, IEnumerable<string>? tags = null, string visibility = "PRIVATE", CancellationToken cancellationToken = default);
    Task<BookmarkRecord> CreatePlacementAsync(string system, string placementId, string? name = null, IEnumerable<string>? tags = null, string visibility = "PRIVATE", CancellationToken cancellationToken = default);
    Task<BookmarkRecord> CreateSurfaceAnchorAsync(string system, string anchorId, string? name = null, IEnumerable<string>? tags = null, string visibility = "PRIVATE", CancellationToken cancellationToken = default);
    Task<BookmarkRecord> CreateBodyFixedPointAsync(string name, string system, string bodyName, Vector3D point, string? bodySourceUuid, double? latitudeDegrees = null, double? longitudeDegrees = null, double? altitudeMeters = null, IEnumerable<string>? tags = null, string visibility = "PRIVATE", CancellationToken cancellationToken = default);
    Task<BookmarkRecord> UpdateMetadataAsync(Guid id, string name, IEnumerable<string>? tags = null, string visibility = "PRIVATE", string? category = null, string? note = null, string? color = null, bool? showOnMap = null, string? bookmarkScope = null, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class BookmarkService : IBookmarkService
{
    private readonly IUniverseService _universe;
    private readonly LocalBookmarkStore _store;

    public BookmarkService(IUniverseService universe, LocalBookmarkStore? store = null)
    {
        _universe = universe;
        _store = store ?? new LocalBookmarkStore();
    }

    public Task<IReadOnlyList<BookmarkRecord>> ListAsync(CancellationToken cancellationToken = default) =>
        _store.LoadAsync(cancellationToken);

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        _store.DeleteAsync(id, cancellationToken);

    public async Task<BookmarkRecord> UpdateMetadataAsync(
        Guid id,
        string name,
        IEnumerable<string>? tags = null,
        string visibility = "PRIVATE",
        string? category = null,
        string? note = null,
        string? color = null,
        bool? showOnMap = null,
        string? bookmarkScope = null,
        CancellationToken cancellationToken = default)
    {
        var existing = (await _store.LoadAsync(cancellationToken)).FirstOrDefault(b => b.Id == id)
            ?? throw new KeyNotFoundException($"Unknown bookmark '{id}'.");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Bookmark name is required.", nameof(name));
        var normalizedColor = string.IsNullOrWhiteSpace(color) ? existing.Color : color.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalizedColor, "^#[0-9a-fA-F]{6}$"))
            throw new ArgumentException("Bookmark color must be #RRGGBB.", nameof(color));
        var updated = existing with
        {
            Name = name.Trim(),
            Tags = (tags ?? existing.Tags).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Visibility = string.IsNullOrWhiteSpace(visibility) ? existing.Visibility : visibility.Trim().ToUpperInvariant(),
            Category = string.IsNullOrWhiteSpace(category) ? existing.Category : category.Trim(),
            Note = note ?? existing.Note,
            Color = normalizedColor,
            ShowOnMap = showOnMap ?? existing.ShowOnMap,
            BookmarkScope = string.IsNullOrWhiteSpace(bookmarkScope) ? existing.BookmarkScope : bookmarkScope.Trim().ToUpperInvariant()
        };
        return await _store.UpsertAsync(updated, cancellationToken);
    }

    public async Task<BookmarkRecord> CreateSystemPointAsync(
        string name,
        string system,
        Vector3D point,
        IEnumerable<string>? tags = null,
        string visibility = "PRIVATE",
        CancellationToken cancellationToken = default)
    {
        _ = _universe.GetSystem(system);
        var bookmark = BookmarkFactory.FromSystemPoint(name, system, point, tags, visibility);
        return await _store.UpsertAsync(bookmark, cancellationToken);
    }

    public async Task<BookmarkRecord> CreatePlacementAsync(
        string system,
        string placementId,
        string? name = null,
        IEnumerable<string>? tags = null,
        string visibility = "PRIVATE",
        CancellationToken cancellationToken = default)
    {
        var dataset = _universe.GetSystem(system);
        var entity = dataset.Entities.FirstOrDefault(e => e.Id.Equals(placementId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Unknown placement '{placementId}' in {system}.");
        var bookmark = BookmarkFactory.FromPlacement(dataset, entity, name, tags, visibility);
        return await _store.UpsertAsync(bookmark, cancellationToken);
    }

    public async Task<BookmarkRecord> CreateSurfaceAnchorAsync(
        string system,
        string anchorId,
        string? name = null,
        IEnumerable<string>? tags = null,
        string visibility = "PRIVATE",
        CancellationToken cancellationToken = default)
    {
        var dataset = _universe.GetSystem(system);
        var anchor = dataset.BodyAnchors.FirstOrDefault(a => a.AnchorId.Equals(anchorId, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Unknown body anchor '{anchorId}' in {system}.");
        if (!dataset.Frames.Any(f => f.FrameId.Equals(anchor.BodyFixedFrameId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Anchor '{anchor.Name}' references missing frame '{anchor.BodyFixedFrameId}'.");
        var bookmark = BookmarkFactory.FromSurfaceAnchor(anchor, name, tags, visibility);
        return await _store.UpsertAsync(bookmark, cancellationToken);
    }

    public async Task<BookmarkRecord> CreateBodyFixedPointAsync(
        string name,
        string system,
        string bodyName,
        Vector3D point,
        string? bodySourceUuid,
        double? latitudeDegrees = null,
        double? longitudeDegrees = null,
        double? altitudeMeters = null,
        IEnumerable<string>? tags = null,
        string visibility = "PRIVATE",
        CancellationToken cancellationToken = default)
    {
        var dataset = _universe.GetSystem(system);
        var body = dataset.Bodies.FirstOrDefault(b =>
            (!string.IsNullOrWhiteSpace(bodySourceUuid) && b.SourceUuid?.Equals(bodySourceUuid, StringComparison.OrdinalIgnoreCase) == true) ||
            b.Name.Equals(bodyName, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Unknown physical body '{bodyName}' in {system}.");
        var frameId = dataset.BodyAnchors.FirstOrDefault(a =>
                a.BodyName.Equals(body.Name, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(body.SourceUuid) && a.BodySourceUuid?.Equals(body.SourceUuid, StringComparison.OrdinalIgnoreCase) == true))?.BodyFixedFrameId
            ?? throw new InvalidOperationException($"No proven BodyFixed frame is available for '{body.Name}'.");
        if (!dataset.Frames.Any(f => f.FrameId.Equals(frameId, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"BodyFixed frame '{frameId}' for '{body.Name}' is missing from the frame graph.");
        var bookmark = BookmarkFactory.FromBodyFixedPoint(name, system, frameId, point, body.SourceUuid,
            latitudeDegrees, longitudeDegrees, altitudeMeters, tags, visibility);
        return await _store.UpsertAsync(bookmark, cancellationToken);
    }
}

using System.Text.Json;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public sealed class LocalBookmarkStore
{
    private readonly string _path;
    private readonly UniverseSyncHostClientIdentityService _identityService;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public LocalBookmarkStore(string? path = null, UniverseSyncHostClientIdentityService? identityService = null)
    {
        _identityService = identityService ?? new UniverseSyncHostClientIdentityService();
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StarSyncUniverse", "UserData", "bookmarks.json");
    }

    public string PathOnDisk => _path;

    public async Task<IReadOnlyList<BookmarkRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path)) return [];
        await using var stream = File.OpenRead(_path);
        var loaded = await JsonSerializer.DeserializeAsync<List<BookmarkRecord>>(stream, JsonOptions, cancellationToken) ?? [];
        return loaded.Select(NormalizeLegacy).Select(EnsureIntegrity).ToArray();
    }

    private static BookmarkRecord NormalizeLegacy(BookmarkRecord bookmark)
    {
        var legacyPresentationMissing = string.IsNullOrWhiteSpace(bookmark.Category) || string.IsNullOrWhiteSpace(bookmark.Color) || string.IsNullOrWhiteSpace(bookmark.BookmarkScope);
        return bookmark with
        {
            Category = string.IsNullOrWhiteSpace(bookmark.Category) ? "Infos" : bookmark.Category,
            Note = bookmark.Note ?? string.Empty,
            Color = string.IsNullOrWhiteSpace(bookmark.Color) ? "#f59e0b" : bookmark.Color,
            ShowOnMap = legacyPresentationMissing ? true : bookmark.ShowOnMap,
            BookmarkScope = string.IsNullOrWhiteSpace(bookmark.BookmarkScope)
                ? (bookmark.CoordinateKind.Equals("BODY_FIXED_XYZ", StringComparison.OrdinalIgnoreCase) ? "SURFACE" : "SPACE")
                : bookmark.BookmarkScope
        };
    }

    private BookmarkRecord EnsureIntegrity(BookmarkRecord bookmark)
    {
        var creator = string.IsNullOrWhiteSpace(bookmark.CreatorClientId)
            ? _identityService.GetOrCreate().ClientId
            : bookmark.CreatorClientId.Trim();
        var normalized = bookmark with { CreatorClientId = creator };
        return normalized with { ContentFingerprint = BookmarkIntegrity.ComputeBookmarkFingerprint(normalized) };
    }

    public async Task<BookmarkRecord> UpsertAsync(BookmarkRecord bookmark, CancellationToken cancellationToken = default)
    {
        bookmark = EnsureIntegrity(bookmark);
        Validate(bookmark);
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            var all = (await LoadAsync(cancellationToken)).ToList();
            var index = all.FindIndex(b => b.Id == bookmark.Id);
            BookmarkRecord stored;
            if (index >= 0)
            {
                var existing = all[index];
                stored = bookmark with
                {
                    CreatorClientId = string.IsNullOrWhiteSpace(existing.CreatorClientId) ? bookmark.CreatorClientId : existing.CreatorClientId,
                    CreatedUtc = existing.CreatedUtc,
                    UpdatedUtc = DateTimeOffset.UtcNow,
                    Revision = Math.Max(existing.Revision + 1, bookmark.Revision + 1)
                };
                stored = EnsureIntegrity(stored);
                all[index] = stored;
            }
            else
            {
                var now = DateTimeOffset.UtcNow;
                stored = bookmark with
                {
                    CreatedUtc = bookmark.CreatedUtc == default ? now : bookmark.CreatedUtc,
                    UpdatedUtc = now,
                    Revision = Math.Max(1, bookmark.Revision)
                };
                all.Add(stored);
            }
            await SaveAllAsync(all, cancellationToken);
            return stored;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            var all = (await LoadAsync(cancellationToken)).ToList();
            var removed = all.RemoveAll(b => b.Id == id) > 0;
            if (removed) await SaveAllAsync(all, cancellationToken);
            return removed;
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private async Task SaveAllAsync(IReadOnlyList<BookmarkRecord> bookmarks, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path) ?? throw new InvalidOperationException("Bookmark path has no directory.");
        Directory.CreateDirectory(directory);
        var temp = _path + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous))
            await JsonSerializer.SerializeAsync(stream, bookmarks, JsonOptions, cancellationToken);

        if (File.Exists(_path)) File.Replace(temp, _path, null, ignoreMetadataErrors: true);
        else File.Move(temp, _path);
    }

    private static void Validate(BookmarkRecord bookmark)
    {
        if (bookmark.Id == Guid.Empty) throw new ArgumentException("Bookmark ID must not be empty.");
        if (string.IsNullOrWhiteSpace(bookmark.Name)) throw new ArgumentException("Bookmark name is required.");
        if (string.IsNullOrWhiteSpace(bookmark.System)) throw new ArgumentException("Bookmark system is required.");
        if (string.IsNullOrWhiteSpace(bookmark.ReferenceFrameId)) throw new ArgumentException("Bookmark reference frame is required.");
        if (!double.IsFinite(bookmark.X) || !double.IsFinite(bookmark.Y) || !double.IsFinite(bookmark.Z))
            throw new ArgumentException("Bookmark XYZ must be finite.");
    }
}

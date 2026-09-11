using System.Text;
using System.Text.Json;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public sealed class BookmarkGroupService
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly LocalBookmarkStore _bookmarkStore;
    private readonly UniverseSyncHostClientIdentityService _identityService;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public BookmarkGroupService(
        LocalBookmarkStore? bookmarkStore = null,
        UniverseSyncHostClientIdentityService? identityService = null,
        string? path = null)
    {
        _identityService = identityService ?? new UniverseSyncHostClientIdentityService();
        _bookmarkStore = bookmarkStore ?? new LocalBookmarkStore(identityService: _identityService);
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StarSyncUniverse", "UserData", "bookmark-groups.json");
    }

    public async Task<IReadOnlyList<BookmarkGroupRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        var groups = await LoadRawAsync(cancellationToken);
        var bookmarks = await _bookmarkStore.LoadAsync(cancellationToken);
        return groups.Select(g => Normalize(g, bookmarks)).OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public async Task<BookmarkGroupRecord> CreateAsync(string name, string? description = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Group name is required.", nameof(name));
        var identity = _identityService.GetOrCreate();
        var now = DateTimeOffset.UtcNow;
        var group = new BookmarkGroupRecord(
            Guid.NewGuid(), name.Trim(), description?.Trim() ?? string.Empty, [], identity.ClientId,
            1, now, now, string.Empty, "LOCAL");
        return await UpsertAsync(group, cancellationToken);
    }

    public async Task<BookmarkGroupRecord> UpdateMetadataAsync(Guid id, string name, string? description, CancellationToken cancellationToken = default)
    {
        var groups = await LoadRawAsync(cancellationToken);
        var existing = groups.FirstOrDefault(x => x.Id == id) ?? throw new KeyNotFoundException($"Unknown bookmark group '{id}'.");
        return await UpsertAsync(existing with
        {
            Name = string.IsNullOrWhiteSpace(name) ? existing.Name : name.Trim(),
            Description = description?.Trim() ?? existing.Description
        }, cancellationToken);
    }

    public async Task<BookmarkGroupRecord> AssignAsync(Guid groupId, IEnumerable<Guid> bookmarkIds, CancellationToken cancellationToken = default)
    {
        var groups = await LoadRawAsync(cancellationToken);
        var group = groups.FirstOrDefault(x => x.Id == groupId) ?? throw new KeyNotFoundException($"Unknown bookmark group '{groupId}'.");
        var existingBookmarkIds = (await _bookmarkStore.LoadAsync(cancellationToken)).Select(x => x.Id).ToHashSet();
        var ids = group.BookmarkIds.Concat(bookmarkIds).Where(existingBookmarkIds.Contains).Distinct().OrderBy(x => x).ToArray();
        return await UpsertAsync(group with { BookmarkIds = ids }, cancellationToken);
    }

    public async Task<BookmarkGroupRecord> RemoveAsync(Guid groupId, IEnumerable<Guid> bookmarkIds, CancellationToken cancellationToken = default)
    {
        var groups = await LoadRawAsync(cancellationToken);
        var group = groups.FirstOrDefault(x => x.Id == groupId) ?? throw new KeyNotFoundException($"Unknown bookmark group '{groupId}'.");
        var remove = bookmarkIds.ToHashSet();
        return await UpsertAsync(group with { BookmarkIds = group.BookmarkIds.Where(x => !remove.Contains(x)).Distinct().OrderBy(x => x).ToArray() }, cancellationToken);
    }

    public async Task<BookmarkGroupRecord> MoveAsync(Guid sourceGroupId, Guid targetGroupId, IEnumerable<Guid> bookmarkIds, CancellationToken cancellationToken = default)
    {
        var ids = bookmarkIds.Distinct().ToArray();
        if (sourceGroupId != Guid.Empty && sourceGroupId != targetGroupId)
            await RemoveAsync(sourceGroupId, ids, cancellationToken);
        return await AssignAsync(targetGroupId, ids, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var groups = await LoadRawAsync(cancellationToken);
            var removed = groups.RemoveAll(x => x.Id == id) > 0;
            if (removed) await SaveAllAsync(groups, cancellationToken);
            return removed;
        }
        finally { _gate.Release(); }
    }

    public async Task<BookmarkGroupPackage> BuildPackageAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var group = (await ListAsync(cancellationToken)).FirstOrDefault(x => x.Id == groupId)
            ?? throw new KeyNotFoundException($"Unknown bookmark group '{groupId}'.");
        var bookmarks = (await _bookmarkStore.LoadAsync(cancellationToken)).Where(x => group.BookmarkIds.Contains(x.Id)).OrderBy(x => x.Id).ToArray();
        var fingerprint = BookmarkIntegrity.ComputeGroupFingerprint(group, bookmarks);
        var identity = _identityService.GetOrCreate();
        var exportedUtc = DateTimeOffset.UtcNow;
        var signingBytes = BuildPackageSigningBytes(group.Id, group.CreatorClientId, identity.ClientId, fingerprint, exportedUtc);
        return new BookmarkGroupPackage(
            "starsync.universe.bookmark-group.v1",
            group with { ContentFingerprint = fingerprint },
            bookmarks,
            identity.ClientId,
            fingerprint,
            exportedUtc,
            identity.SignatureAlgorithm,
            identity.PublicKeySpkiBase64,
            _identityService.Sign(identity, signingBytes));
    }

    public async Task<BookmarkGroupRecord> ImportPackageAsync(BookmarkGroupPackage package, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(package.Schema, "starsync.universe.bookmark-group.v1", StringComparison.Ordinal))
            throw new InvalidDataException("Unsupported bookmark-group package schema.");
        var expected = BookmarkIntegrity.ComputeGroupFingerprint(package.Group, package.Bookmarks);
        if (!string.Equals(expected, package.ContentFingerprint, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Bookmark-group fingerprint validation failed.");
        var signingBytes = BuildPackageSigningBytes(package.Group.Id, package.Group.CreatorClientId, package.PublisherClientId, package.ContentFingerprint, package.ExportedUtc);
        if (!string.Equals(package.SignatureAlgorithm, UniverseSyncHostClientIdentityService.SignatureAlgorithm, StringComparison.OrdinalIgnoreCase) ||
            !UniverseSyncHostClientIdentityService.Verify(package.SignerPublicKeySpkiBase64, signingBytes, package.Signature))
            throw new InvalidDataException("Bookmark-group client signature validation failed.");

        // Stable IDs are authoritative for de-duplication. Imported content never rewrites the original creator.
        var localBookmarks = (await _bookmarkStore.LoadAsync(cancellationToken)).ToDictionary(x => x.Id);
        foreach (var incoming in package.Bookmarks)
        {
            if (localBookmarks.ContainsKey(incoming.Id)) continue;
            await _bookmarkStore.UpsertAsync(incoming, cancellationToken);
        }
        return await UpsertAsync(package.Group with { ShareStatus = "IMPORTED" }, cancellationToken, preserveIncomingCreator: true);
    }

    private async Task<BookmarkGroupRecord> UpsertAsync(BookmarkGroupRecord group, CancellationToken cancellationToken, bool preserveIncomingCreator = false)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var groups = await LoadRawAsync(cancellationToken);
            var bookmarks = await _bookmarkStore.LoadAsync(cancellationToken);
            var index = groups.FindIndex(x => x.Id == group.Id);
            var now = DateTimeOffset.UtcNow;
            BookmarkGroupRecord stored;
            if (index >= 0)
            {
                var existing = groups[index];
                stored = group with
                {
                    CreatorClientId = preserveIncomingCreator && !string.IsNullOrWhiteSpace(group.CreatorClientId)
                        ? group.CreatorClientId
                        : existing.CreatorClientId,
                    CreatedUtc = existing.CreatedUtc,
                    UpdatedUtc = now,
                    Revision = Math.Max(existing.Revision + 1, group.Revision + 1)
                };
                stored = Normalize(stored, bookmarks);
                groups[index] = stored;
            }
            else
            {
                stored = group with
                {
                    CreatorClientId = !string.IsNullOrWhiteSpace(group.CreatorClientId)
                        ? group.CreatorClientId
                        : _identityService.GetOrCreate().ClientId,
                    CreatedUtc = group.CreatedUtc == default ? now : group.CreatedUtc,
                    UpdatedUtc = now,
                    Revision = Math.Max(1, group.Revision)
                };
                stored = Normalize(stored, bookmarks);
                groups.Add(stored);
            }
            await SaveAllAsync(groups, cancellationToken);
            return stored;
        }
        finally { _gate.Release(); }
    }

    private BookmarkGroupRecord Normalize(BookmarkGroupRecord group, IReadOnlyList<BookmarkRecord> bookmarks)
    {
        var validIds = bookmarks.Select(x => x.Id).ToHashSet();
        var normalized = group with
        {
            Name = string.IsNullOrWhiteSpace(group.Name) ? "Bookmark Group" : group.Name.Trim(),
            Description = group.Description?.Trim() ?? string.Empty,
            CreatorClientId = string.IsNullOrWhiteSpace(group.CreatorClientId) ? _identityService.GetOrCreate().ClientId : group.CreatorClientId.Trim(),
            BookmarkIds = (group.BookmarkIds ?? []).Where(validIds.Contains).Distinct().OrderBy(x => x).ToArray(),
            ShareStatus = string.IsNullOrWhiteSpace(group.ShareStatus) ? "LOCAL" : group.ShareStatus.Trim().ToUpperInvariant()
        };
        return normalized with { ContentFingerprint = BookmarkIntegrity.ComputeGroupFingerprint(normalized, bookmarks) };
    }

    private static byte[] BuildPackageSigningBytes(Guid groupId, string creatorClientId, string publisherClientId, string fingerprint, DateTimeOffset exportedUtc)
    {
        var input = string.Join("\n", new[]
        {
            "starsync-universe-bookmark-group-v1",
            groupId.ToString("D"),
            creatorClientId ?? string.Empty,
            publisherClientId ?? string.Empty,
            fingerprint ?? string.Empty,
            exportedUtc.ToUniversalTime().ToString("O")
        });
        return Encoding.UTF8.GetBytes(input);
    }

    private async Task<List<BookmarkGroupRecord>> LoadRawAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path)) return [];
        try
        {
            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<List<BookmarkGroupRecord>>(stream, JsonOptions, cancellationToken) ?? [];
        }
        catch { return []; }
    }

    private async Task SaveAllAsync(IReadOnlyList<BookmarkGroupRecord> groups, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path) ?? throw new InvalidOperationException("Bookmark group path has no directory.");
        Directory.CreateDirectory(directory);
        var temp = _path + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous))
            await JsonSerializer.SerializeAsync(stream, groups, JsonOptions, cancellationToken);
        if (File.Exists(_path)) File.Replace(temp, _path, null, ignoreMetadataErrors: true);
        else File.Move(temp, _path);
    }
}

using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class OverlayMergeService
{
    public static OverlayMergeResult Merge(UniverseOverlayEnvelope local, UniverseOverlayEnvelope remote)
    {
        if (!string.Equals(local.SchemaVersion, remote.SchemaVersion, StringComparison.Ordinal))
            throw new InvalidOperationException($"Overlay schema mismatch: local={local.SchemaVersion}, remote={remote.SchemaVersion}.");

        var localWins = 0;
        var remoteWins = 0;
        var equal = 0;
        var conflicts = new List<OverlayConflictRecord>();

        var tombstones = MergeTombstones(local.Tombstones, remote.Tombstones, conflicts, ref localWins, ref remoteWins, ref equal);
        var tombstoneMap = tombstones.ToDictionary(x => TombstoneKey(x.EntityKind, x.Id), StringComparer.OrdinalIgnoreCase);

        var bookmarks = MergeById("Bookmark", local.Bookmarks, remote.Bookmarks, x => x.Id, x => x.Revision, x => x.UpdatedUtc, conflicts, ref localWins, ref remoteWins, ref equal)
            .Where(x => !IsDeleted("Bookmark", x.Id, x.Revision, tombstoneMap)).ToArray();
        var routes = MergeById("Route", local.Routes, remote.Routes, x => x.Id, x => x.Revision, x => x.UpdatedUtc, conflicts, ref localWins, ref remoteWins, ref equal)
            .Where(x => !IsDeleted("Route", x.Id, x.Revision, tombstoneMap)).ToArray();
        var notes = MergeById("Note", local.Notes, remote.Notes, x => x.Id, x => x.Revision, x => x.UpdatedUtc, conflicts, ref localWins, ref remoteWins, ref equal)
            .Where(x => !IsDeleted("Note", x.Id, x.Revision, tombstoneMap)).ToArray();
        var media = MergeById("MediaReference", local.MediaReferences, remote.MediaReferences, x => x.Id, x => x.Revision, x => x.UpdatedUtc, conflicts, ref localWins, ref remoteWins, ref equal)
            .Where(x => !IsDeleted("MediaReference", x.Id, x.Revision, tombstoneMap)).ToArray();

        AddDeleteConflicts("Bookmark", local.Bookmarks.Select(x => (x.Id, x.Revision)), remote.Bookmarks.Select(x => (x.Id, x.Revision)), tombstoneMap, conflicts);
        AddDeleteConflicts("Route", local.Routes.Select(x => (x.Id, x.Revision)), remote.Routes.Select(x => (x.Id, x.Revision)), tombstoneMap, conflicts);
        AddDeleteConflicts("Note", local.Notes.Select(x => (x.Id, x.Revision)), remote.Notes.Select(x => (x.Id, x.Revision)), tombstoneMap, conflicts);
        AddDeleteConflicts("MediaReference", local.MediaReferences.Select(x => (x.Id, x.Revision)), remote.MediaReferences.Select(x => (x.Id, x.Revision)), tombstoneMap, conflicts);

        var merged = new UniverseOverlayEnvelope(
            local.SchemaVersion,
            local.SourceBuild ?? remote.SourceBuild,
            Math.Max(local.Revision, remote.Revision) + 1,
            bookmarks,
            routes,
            notes,
            media,
            tombstones,
            DateTimeOffset.UtcNow);

        return new OverlayMergeResult(
            merged,
            localWins,
            remoteWins,
            equal,
            conflicts.OrderBy(x => x.EntityKind, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id).ToArray(),
            "DETERMINISTIC_REVISION_THEN_TIMESTAMP_WITH_TOMBSTONES_AND_CONFLICT_TELEMETRY");
    }

    private static IReadOnlyList<OverlayTombstoneRecord> MergeTombstones(
        IReadOnlyList<OverlayTombstoneRecord> local,
        IReadOnlyList<OverlayTombstoneRecord> remote,
        ICollection<OverlayConflictRecord> conflicts,
        ref int localWins,
        ref int remoteWins,
        ref int equal)
    {
        var localMap = local.ToDictionary(x => TombstoneKey(x.EntityKind, x.Id), StringComparer.OrdinalIgnoreCase);
        var remoteMap = remote.ToDictionary(x => TombstoneKey(x.EntityKind, x.Id), StringComparer.OrdinalIgnoreCase);
        var keys = localMap.Keys.Concat(remoteMap.Keys).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        var result = new List<OverlayTombstoneRecord>();
        foreach (var key in keys)
        {
            var hasLocal = localMap.TryGetValue(key, out var left);
            var hasRemote = remoteMap.TryGetValue(key, out var right);
            if (hasLocal && !hasRemote) { result.Add(left!); localWins++; continue; }
            if (!hasLocal && hasRemote) { result.Add(right!); remoteWins++; continue; }

            var kind = left!.EntityKind;
            var id = left.Id;
            if (left.Revision > right!.Revision)
            {
                result.Add(left); localWins++;
                conflicts.Add(new OverlayConflictRecord(kind + "Tombstone", id, left.Revision, right.Revision, "LOCAL", "REVISION"));
            }
            else if (right.Revision > left.Revision)
            {
                result.Add(right); remoteWins++;
                conflicts.Add(new OverlayConflictRecord(kind + "Tombstone", id, left.Revision, right.Revision, "REMOTE", "REVISION"));
            }
            else if (left.DeletedUtc > right.DeletedUtc)
            {
                result.Add(left); localWins++;
                conflicts.Add(new OverlayConflictRecord(kind + "Tombstone", id, left.Revision, right.Revision, "LOCAL", "DELETED_UTC"));
            }
            else if (right.DeletedUtc > left.DeletedUtc)
            {
                result.Add(right); remoteWins++;
                conflicts.Add(new OverlayConflictRecord(kind + "Tombstone", id, left.Revision, right.Revision, "REMOTE", "DELETED_UTC"));
            }
            else
            {
                result.Add(left); equal++;
            }
        }
        return result;
    }

    private static bool IsDeleted(string entityKind, Guid id, long recordRevision, IReadOnlyDictionary<string, OverlayTombstoneRecord> tombstones) =>
        tombstones.TryGetValue(TombstoneKey(entityKind, id), out var tombstone) && tombstone.Revision >= recordRevision;

    private static void AddDeleteConflicts(
        string entityKind,
        IEnumerable<(Guid Id, long Revision)> local,
        IEnumerable<(Guid Id, long Revision)> remote,
        IReadOnlyDictionary<string, OverlayTombstoneRecord> tombstones,
        ICollection<OverlayConflictRecord> conflicts)
    {
        foreach (var record in local.Concat(remote).GroupBy(x => x.Id).Select(g => g.OrderByDescending(x => x.Revision).First()))
        {
            if (!tombstones.TryGetValue(TombstoneKey(entityKind, record.Id), out var tombstone) || tombstone.Revision < record.Revision)
                continue;
            if (conflicts.Any(x => x.EntityKind.Equals(entityKind, StringComparison.OrdinalIgnoreCase) && x.Id == record.Id && x.Reason == "TOMBSTONE"))
                continue;
            conflicts.Add(new OverlayConflictRecord(entityKind, record.Id, record.Revision, tombstone.Revision, "TOMBSTONE", "TOMBSTONE"));
        }
    }

    private static string TombstoneKey(string entityKind, Guid id) => $"{entityKind}:{id:D}";

    private static IReadOnlyList<T> MergeById<T>(
        string entityKind,
        IReadOnlyList<T> local,
        IReadOnlyList<T> remote,
        Func<T, Guid> id,
        Func<T, long> revision,
        Func<T, DateTimeOffset> updatedUtc,
        ICollection<OverlayConflictRecord> conflicts,
        ref int localWins,
        ref int remoteWins,
        ref int equal)
    {
        var localMap = local.ToDictionary(id);
        var remoteMap = remote.ToDictionary(id);
        var allIds = localMap.Keys.Concat(remoteMap.Keys).Distinct().OrderBy(x => x).ToArray();
        var result = new List<T>(allIds.Length);

        foreach (var key in allIds)
        {
            var hasLocal = localMap.TryGetValue(key, out var left);
            var hasRemote = remoteMap.TryGetValue(key, out var right);
            if (hasLocal && !hasRemote)
            {
                result.Add(left!);
                localWins++;
                continue;
            }
            if (!hasLocal && hasRemote)
            {
                result.Add(right!);
                remoteWins++;
                continue;
            }

            var lr = revision(left!);
            var rr = revision(right!);
            if (lr > rr)
            {
                result.Add(left!);
                localWins++;
                conflicts.Add(new OverlayConflictRecord(entityKind, key, lr, rr, "LOCAL", "REVISION"));
            }
            else if (rr > lr)
            {
                result.Add(right!);
                remoteWins++;
                conflicts.Add(new OverlayConflictRecord(entityKind, key, lr, rr, "REMOTE", "REVISION"));
            }
            else
            {
                var lt = updatedUtc(left!);
                var rt = updatedUtc(right!);
                if (lt > rt)
                {
                    result.Add(left!);
                    localWins++;
                    conflicts.Add(new OverlayConflictRecord(entityKind, key, lr, rr, "LOCAL", "UPDATED_UTC"));
                }
                else if (rt > lt)
                {
                    result.Add(right!);
                    remoteWins++;
                    conflicts.Add(new OverlayConflictRecord(entityKind, key, lr, rr, "REMOTE", "UPDATED_UTC"));
                }
                else
                {
                    result.Add(left!);
                    equal++;
                }
            }
        }

        return result;
    }
}

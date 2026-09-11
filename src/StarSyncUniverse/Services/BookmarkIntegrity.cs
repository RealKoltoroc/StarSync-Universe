using System.Security.Cryptography;
using System.Text;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class BookmarkIntegrity
{
    public static string ComputeBookmarkFingerprint(BookmarkRecord bookmark)
    {
        var payload = string.Join("\n", new[]
        {
            bookmark.Id.ToString("D"),
            bookmark.CreatorClientId ?? string.Empty,
            bookmark.Name ?? string.Empty,
            bookmark.System ?? string.Empty,
            bookmark.ReferenceFrameId ?? string.Empty,
            bookmark.CoordinateKind ?? string.Empty,
            bookmark.X.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            bookmark.Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            bookmark.Z.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
            bookmark.BodySourceUuid ?? string.Empty,
            bookmark.LatitudeDegrees?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            bookmark.LongitudeDegrees?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            bookmark.AltitudeMeters?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
            string.Join("|", (bookmark.Tags ?? []).OrderBy(x => x, StringComparer.OrdinalIgnoreCase)),
            bookmark.Visibility ?? string.Empty,
            bookmark.Category ?? string.Empty,
            bookmark.Note ?? string.Empty,
            bookmark.Color ?? string.Empty,
            bookmark.ShowOnMap ? "1" : "0",
            bookmark.BookmarkScope ?? string.Empty
        });
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public static string ComputeGroupFingerprint(BookmarkGroupRecord group, IEnumerable<BookmarkRecord> bookmarks)
    {
        var byId = bookmarks.ToDictionary(x => x.Id);
        var lines = new List<string>
        {
            group.Id.ToString("D"),
            group.CreatorClientId ?? string.Empty,
            group.Name ?? string.Empty,
            group.Description ?? string.Empty
        };
        foreach (var id in (group.BookmarkIds ?? []).Distinct().OrderBy(x => x))
        {
            lines.Add(id.ToString("D"));
            lines.Add(byId.TryGetValue(id, out var bookmark)
                ? (string.IsNullOrWhiteSpace(bookmark.ContentFingerprint) ? ComputeBookmarkFingerprint(bookmark) : bookmark.ContentFingerprint)
                : "missing");
        }
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines)))).ToLowerInvariant();
    }
}

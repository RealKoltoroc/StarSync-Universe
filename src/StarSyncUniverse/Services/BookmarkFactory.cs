using StarSyncUniverse.Domain;
using StarSyncUniverse.Transforms;

namespace StarSyncUniverse.Services;

public static class BookmarkFactory
{
    public static BookmarkRecord FromSystemPoint(string name, string system, Vector3D point, IEnumerable<string>? tags = null, string visibility = "PRIVATE") =>
        Create(name, system, $"system:{system.ToLowerInvariant()}", "SYSTEM_XYZ", point, null, null, null, null, tags, visibility);

    public static BookmarkRecord FromPlacement(UniverseDataset dataset, UniverseEntity entity, string? name = null, IEnumerable<string>? tags = null, string visibility = "PRIVATE")
    {
        if (!entity.System.Equals(dataset.System, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Placement does not belong to the supplied dataset.");
        return Create(name ?? entity.Name, dataset.System, $"system:{dataset.System.ToLowerInvariant()}", "PLACEMENT_SNAPSHOT_XYZ",
            new Vector3D(entity.X, entity.Y, entity.Z), null, null, null, null, tags, visibility);
    }

    public static BookmarkRecord FromSurfaceAnchor(BodyLocalAnchorRecord anchor, string? name = null, IEnumerable<string>? tags = null, string visibility = "PRIVATE") =>
        Create(name ?? anchor.Name, anchor.System, anchor.BodyFixedFrameId, "BODY_FIXED_XYZ",
            new Vector3D(anchor.BodyLocalX, anchor.BodyLocalY, anchor.BodyLocalZ), anchor.BodySourceUuid,
            anchor.AxisLatitudeDegrees, anchor.AxisLongitudeDegrees, anchor.AltitudeMeters, tags, visibility);

    public static BookmarkRecord FromBodyFixedPoint(string name, string system, string frameId, Vector3D point, string? bodySourceUuid,
        double? latitudeDegrees, double? longitudeDegrees, double? altitudeMeters, IEnumerable<string>? tags = null, string visibility = "PRIVATE") =>
        Create(name, system, frameId, "BODY_FIXED_XYZ", point, bodySourceUuid, latitudeDegrees, longitudeDegrees, altitudeMeters, tags, visibility);

    private static BookmarkRecord Create(string name, string system, string frameId, string kind, Vector3D point,
        string? bodyUuid, double? lat, double? lon, double? alt, IEnumerable<string>? tags, string visibility)
    {
        var now = DateTimeOffset.UtcNow;
        var scope = kind.Equals("BODY_FIXED_XYZ", StringComparison.OrdinalIgnoreCase) ? "SURFACE" : "SPACE";
        return new BookmarkRecord(Guid.NewGuid(), name, system.ToLowerInvariant(), frameId, kind,
            point.X, point.Y, point.Z, bodyUuid, lat, lon, alt,
            (tags ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            visibility, 1, now, now, "Infos", "", "#f59e0b", true, scope);
    }
}

using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class SurfaceCoverageBuilder
{
    public static IReadOnlyList<SurfaceCoverageSummaryRecord> Build(UniverseDataset dataset)
    {
        return dataset.Bodies
            .OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .Select(body =>
            {
                var anchors = dataset.BodyAnchors
                    .Where(a => string.Equals(a.BodyName, body.Name, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                return new SurfaceCoverageSummaryRecord(
                    dataset.System,
                    body.Name,
                    body.SourceUuid,
                    anchors.Length,
                    anchors.Count(a => a.AnchorScope == "NEAR_SURFACE"),
                    anchors.Count(a => a.AnchorScope == "BODY_LOCAL_ATMOSPHERIC_OR_ELEVATED"),
                    anchors.Count(a => a.AnchorScope == "BODY_LOCAL_SPACE_ORBITAL"),
                    anchors.Count(a => a.AnchorScope == "BODY_LOCAL_SUBSURFACE"),
                    anchors.Length == 0 ? null : anchors.Min(a => a.AltitudeMeters),
                    anchors.Length == 0 ? null : anchors.Max(a => a.AltitudeMeters),
                    "BODY_LOCAL_ENGINE_AXES_SPHERICAL_GEODETIC_CONVENTION_UNPROVEN",
                    anchors.Length == 0 ? "CURRENT_LIVE_BODY_DEFINITION_ONLY_NO_SURFACE_ANCHORS" : "LOCAL_DIRECT_XYZ_WITH_DERIVED_ALTITUDE");
            })
            .ToArray();
    }
}

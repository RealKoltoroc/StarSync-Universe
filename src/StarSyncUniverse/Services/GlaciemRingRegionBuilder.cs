using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

/// <summary>
/// Derives a diagnostic annular envelope from CURRENT LIVE Nyx Glaciem segment placements.
/// This is not a claim about density or exact ring edge topology; it replaces the misleading
/// giant background AABB with a placement-derived ring band for spatial inspection.
/// </summary>
public static class GlaciemRingRegionBuilder
{
    public static SpatialRegion? Build(UniverseDataset dataset)
    {
        if (!dataset.System.Equals("nyx", StringComparison.OrdinalIgnoreCase)) return null;
        var star = dataset.Entities.FirstOrDefault(e => e.Type.Equals("Star", StringComparison.OrdinalIgnoreCase));
        if (star is null) return null;

        var segments = dataset.Volumes
            .Where(v => v.Type.Equals("RingSegmentVolume", StringComparison.OrdinalIgnoreCase) &&
                        (v.TemplateSourcePath?.Contains("glaciemring_segment_", StringComparison.OrdinalIgnoreCase) ?? false))
            .ToArray();
        if (segments.Length < 3) return null;

        var radial = segments
            .Select(v => Math.Sqrt(
                Math.Pow(v.CenterX - star.X, 2) +
                Math.Pow(v.CenterY - star.Y, 2) +
                Math.Pow(v.CenterZ - star.Z, 2)))
            .ToArray();
        var maxHalfExtent = segments.Max(v => Math.Max(
            Math.Max(Math.Abs(v.MinLocalX), Math.Abs(v.MaxLocalX)),
            Math.Max(Math.Abs(v.MinLocalY), Math.Abs(v.MaxLocalY))));
        var minZ = segments.Min(v => v.CenterZ + v.MinLocalZ);
        var maxZ = segments.Max(v => v.CenterZ + v.MaxLocalZ);

        return new SpatialRegion(
            Id: "nyx:glaciem:ring-placement-envelope",
            Name: "Glaciem Ring placement envelope",
            Type: "DerivedRingPlacementBand",
            System: dataset.System,
            ParentFrame: $"system:{dataset.System.ToLowerInvariant()}",
            CenterX: star.X,
            CenterY: star.Y,
            CenterZ: star.Z,
            InnerRadiusMeters: Math.Max(0d, radial.Min() - maxHalfExtent),
            OuterRadiusMeters: radial.Max() + maxHalfExtent,
            ThicknessMeters: Math.Max(0d, maxZ - minZ),
            DensityScale: 0d,
            Composition: $"Envelope derived from {segments.Length} CURRENT LIVE Glaciem Ring segment placements; density/topology unresolved",
            SourcePath: "Data/ObjectContainers/PU/system/nyx/glaciemring/glaciemring_segment_*.socpak",
            SourceAuthority: "LOCAL_DERIVED from CURRENT LIVE segment world XYZ + direct segment bounds",
            DataStatus: "LOCAL_DERIVED_PLACEMENT_ENVELOPE_NOT_EXACT_RING_EDGE");
    }
}

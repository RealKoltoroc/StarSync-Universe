using StarSyncUniverse.Domain;
using StarSyncUniverse.Transforms;

namespace StarSyncUniverse.Services;

/// <summary>
/// Immutable per-system AABB index for spatial volumes. The index is built once
/// after import and answers containment queries without scanning every volume.
/// </summary>
public sealed class SystemVolumeIndex
{
    private readonly IndexedVolume[] _byMinX;

    public SystemVolumeIndex(UniverseDataset dataset)
    {
        _byMinX = dataset.Volumes
            .Select(v => new IndexedVolume(
                v,
                v.CenterX + v.MinLocalX,
                v.CenterX + v.MaxLocalX,
                v.CenterY + v.MinLocalY,
                v.CenterY + v.MaxLocalY,
                v.CenterZ + v.MinLocalZ,
                v.CenterZ + v.MaxLocalZ))
            .OrderBy(v => v.MinX)
            .ToArray();
    }

    public int Count => _byMinX.Length;

    public IReadOnlyList<SpatialVolume> FindContaining(Vector3D point)
    {
        if (_byMinX.Length == 0) return [];

        var end = UpperBoundMinX(point.X);
        if (end == 0) return [];

        var results = new List<SpatialVolume>();
        for (var i = 0; i < end; i++)
        {
            var item = _byMinX[i];
            if (point.X > item.MaxX || point.Y < item.MinY || point.Y > item.MaxY || point.Z < item.MinZ || point.Z > item.MaxZ)
                continue;
            results.Add(item.Volume);
        }

        if (results.Count <= 1) return results;
        results.Sort(static (a, b) =>
        {
            var radius = a.BoundingRadiusMeters.CompareTo(b.BoundingRadiusMeters);
            return radius != 0 ? radius : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
        });
        return results;
    }

    private int UpperBoundMinX(double x)
    {
        var lo = 0;
        var hi = _byMinX.Length;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (_byMinX[mid].MinX <= x) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    private sealed record IndexedVolume(
        SpatialVolume Volume,
        double MinX,
        double MaxX,
        double MinY,
        double MaxY,
        double MinZ,
        double MaxZ);
}

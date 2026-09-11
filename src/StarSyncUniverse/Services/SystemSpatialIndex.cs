using StarSyncUniverse.Domain;
using StarSyncUniverse.Transforms;

namespace StarSyncUniverse.Services;

public sealed class SystemSpatialIndex
{
    private readonly UniverseEntity[] _byX;
    private readonly Dictionary<string, UniverseEntity[]> _bySourceUuid;
    private readonly Dictionary<string, UniverseEntity[]> _childrenByParent;
    private readonly Dictionary<string, UniverseEntity[]> _byType;

    public SystemSpatialIndex(UniverseDataset dataset)
    {
        _byX = dataset.Entities.OrderBy(e => e.X).ToArray();
        _bySourceUuid = dataset.Entities
            .Where(e => !string.IsNullOrWhiteSpace(e.SourceUuid))
            .GroupBy(e => e.SourceUuid!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
        _childrenByParent = dataset.Entities
            .Where(e => !string.IsNullOrWhiteSpace(e.ParentId))
            .GroupBy(e => e.ParentId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
        _byType = dataset.Entities
            .GroupBy(e => e.Type, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<UniverseEntity> BySourceUuid(string sourceUuid) =>
        _bySourceUuid.TryGetValue(sourceUuid, out var items) ? items : [];

    public IReadOnlyList<UniverseEntity> ChildrenOf(string placementId) =>
        _childrenByParent.TryGetValue(placementId, out var items) ? items : [];

    public IReadOnlyList<UniverseEntity> ByType(string type) =>
        _byType.TryGetValue(type, out var items) ? items : [];

    public IReadOnlyList<UniverseEntity> FindWithinRadius(Vector3D center, double radiusMeters, bool includeHidden = true)
    {
        if (radiusMeters < 0 || !double.IsFinite(radiusMeters))
            throw new ArgumentOutOfRangeException(nameof(radiusMeters));

        var minX = center.X - radiusMeters;
        var maxX = center.X + radiusMeters;
        var start = LowerBound(minX);
        var end = UpperBound(maxX);
        var r2 = radiusMeters * radiusMeters;
        var results = new List<UniverseEntity>();

        for (var i = start; i < end; i++)
        {
            var e = _byX[i];
            if (!includeHidden && e.Hidden) continue;
            var dx = e.X - center.X;
            var dy = e.Y - center.Y;
            var dz = e.Z - center.Z;
            if (dx * dx + dy * dy + dz * dz <= r2)
                results.Add(e);
        }

        return results;
    }

    public IReadOnlyList<(UniverseEntity Entity, double DistanceMeters)> FindNearest(
        Vector3D center,
        int maxCount,
        double maxRadiusMeters = double.PositiveInfinity,
        bool includeHidden = true)
    {
        if (maxCount <= 0) return [];
        var source = double.IsPositiveInfinity(maxRadiusMeters)
            ? _byX.Where(e => includeHidden || !e.Hidden)
            : FindWithinRadius(center, maxRadiusMeters, includeHidden);

        return source
            .Select(e =>
            {
                var dx = e.X - center.X;
                var dy = e.Y - center.Y;
                var dz = e.Z - center.Z;
                return (Entity: e, DistanceMeters: Math.Sqrt(dx * dx + dy * dy + dz * dz));
            })
            .OrderBy(x => x.DistanceMeters)
            .Take(maxCount)
            .ToArray();
    }

    private int LowerBound(double x)
    {
        var lo = 0;
        var hi = _byX.Length;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (_byX[mid].X < x) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }

    private int UpperBound(double x)
    {
        var lo = 0;
        var hi = _byX.Length;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (_byX[mid].X <= x) lo = mid + 1;
            else hi = mid;
        }
        return lo;
    }
}

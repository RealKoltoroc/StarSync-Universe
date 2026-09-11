using StarSyncUniverse.Contracts;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public sealed class UniverseCatalogApiAdapter : IUniverseCatalogApi
{
    private readonly IUniverseService _universe;
    private readonly IReadOnlyList<IUniverseCatalogEnrichmentProvider> _enrichmentProviders;

    public UniverseCatalogApiAdapter(
        IUniverseService universe,
        IEnumerable<IUniverseCatalogEnrichmentProvider>? enrichmentProviders = null)
    {
        _universe = universe;
        _enrichmentProviders = enrichmentProviders?.ToArray() ?? [];
    }

    public Task<UniverseCatalogCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var categories = _universe.Systems
            .SelectMany(system => _universe.SearchCatalog(system, includeHidden: true, maxResults: int.MaxValue))
            .Select(x => x.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult(new UniverseCatalogCapabilities(
            UniverseCatalogContract.ApiVersion,
            _universe.Systems.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            categories,
            _enrichmentProviders.Select(x => x.ProviderId).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            SupportsSpatialCoordinates: true,
            SupportsSourceUuidLookup: true,
            SupportsEnrichment: _enrichmentProviders.Count > 0,
            SupportsSpatialMeasurements: true,
            SupportsSpatialVolumes: true));
    }

    public async Task<IReadOnlyList<UniverseCatalogObject>> SearchAsync(UniverseCatalogQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var systems = query.Systems is { Count: > 0 }
            ? query.Systems.Where(s => _universe.Systems.Contains(s, StringComparer.OrdinalIgnoreCase)).ToArray()
            : _universe.Systems.ToArray();
        var categories = query.Categories is { Count: > 0 }
            ? new HashSet<string>(query.Categories, StringComparer.OrdinalIgnoreCase)
            : null;
        var limit = Math.Clamp(query.MaxResults, 1, 10_000);
        var core = systems
            .SelectMany(system => _universe.SearchCatalog(system, query.Text, category: null, query.IncludeHidden, maxResults: limit))
            .Where(x => categories is null || categories.Contains(x.Category))
            .OrderBy(x => x.System, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();

        var result = new List<UniverseCatalogObject>(core.Length);
        foreach (var entry in core)
            result.Add(await MapAndEnrichAsync(entry, cancellationToken));
        return result;
    }

    public async Task<UniverseCatalogObject?> GetByPlacementIdAsync(string system, string placementId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entry = _universe.SearchCatalog(system, includeHidden: true, maxResults: int.MaxValue)
            .FirstOrDefault(x => x.PlacementId.Equals(placementId, StringComparison.OrdinalIgnoreCase));
        return entry is null ? null : await MapAndEnrichAsync(entry, cancellationToken);
    }

    public async Task<IReadOnlyList<UniverseCatalogObject>> GetBySourceUuidAsync(string system, string sourceUuid, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = _universe.SearchCatalog(system, includeHidden: true, maxResults: int.MaxValue)
            .Where(x => string.Equals(x.SourceUuid, sourceUuid, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var result = new List<UniverseCatalogObject>(entries.Length);
        foreach (var entry in entries)
            result.Add(await MapAndEnrichAsync(entry, cancellationToken));
        return result;
    }

    public Task<UniverseSpatialPosition> GetPositionAsync(string system, string placementId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var p = _universe.GetPosition(system, placementId);
        return Task.FromResult(new UniverseSpatialPosition(
            p.System, p.PlacementId, p.Name, p.X, p.Y, p.Z,
            p.CoordinateFrame, p.SourceAuthority, p.DataStatus));
    }

    public Task<UniverseSpatialMeasurement> MeasureAsync(string system, string fromPlacementId, string toPlacementId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var m = _universe.Measure(system, fromPlacementId, toPlacementId);
        return Task.FromResult(new UniverseSpatialMeasurement(
            m.System, m.FromPlacementId, m.ToPlacementId, m.FromName, m.ToName,
            m.DeltaX, m.DeltaY, m.DeltaZ, m.DistanceMeters, m.HorizontalDistanceMeters,
            m.ElevationDeltaMeters, m.AzimuthDegreesSystemXY, m.ElevationAngleDegrees,
            m.CoordinateFrame, m.DataStatus));
    }

    public Task<IReadOnlyList<UniverseSpatialVolumeHit>> FindVolumesContainingAsync(string system, double x, double y, double z, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<UniverseSpatialVolumeHit> result = _universe.FindVolumesContaining(system, new StarSyncUniverse.Transforms.Vector3D(x, y, z))
            .Select(v => new UniverseSpatialVolumeHit(
                v.Id, v.Name, v.Type, v.System, v.CenterX, v.CenterY, v.CenterZ,
                v.BoundingRadiusMeters, v.SourceAuthority, v.DataStatus))
            .ToArray();
        return Task.FromResult(result);
    }

    private async Task<UniverseCatalogObject> MapAndEnrichAsync(UniverseCatalogEntry entry, CancellationToken cancellationToken)
    {
        var core = new UniverseCatalogObject(
            entry.System,
            entry.PlacementId,
            entry.SourceUuid,
            entry.Name,
            entry.Category,
            entry.Type,
            entry.EntityClass,
            entry.ParentPlacementId,
            entry.ParentSourceUuid,
            entry.X,
            entry.Y,
            entry.Z,
            entry.Hidden,
            entry.QuantumTravelValid,
            entry.SourcePath,
            entry.SourceAuthority,
            entry.DataStatus,
            []);

        if (_enrichmentProviders.Count == 0)
            return core;

        var enrichments = new List<UniverseEnrichmentDocument>(_enrichmentProviders.Count);
        foreach (var provider in _enrichmentProviders)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var enrichment = await provider.EnrichAsync(core, cancellationToken);
            if (enrichment is not null)
                enrichments.Add(enrichment);
        }
        return core with { Enrichments = enrichments };
    }
}

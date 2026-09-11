using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Data;

/// <summary>
/// Applies cross-build SCUnpacked identity/QT metadata only where CURRENT LIVE Data.p4k
/// already provides the placement UUID and world XYZ and both match the reference exactly
/// within a strict tolerance. No position, transform, or placement geometry is ever copied.
/// </summary>
public sealed class ExactWorldReferenceIdentityEnricher
{
    private readonly ScUnpackedCatalog _catalog;

    public ExactWorldReferenceIdentityEnricher(ScUnpackedCatalog catalog) => _catalog = catalog;

    public int Apply(UniverseDataset dataset, double toleranceMeters = 1d)
    {
        var enriched = 0;
        for (var i = 0; i < dataset.Entities.Count; i++)
        {
            var entity = dataset.Entities[i];
            if (string.IsNullOrWhiteSpace(entity.SourceUuid))
                continue;

            var reference = _catalog.MatchByUuidAndWorld(
                entity.SourceUuid, dataset.System, entity.X, entity.Y, entity.Z, toleranceMeters);
            if (reference is null)
                continue;

            var alreadyReferenceMatched = entity.DataStatus.Contains("REFERENCE_IDENTITY", StringComparison.OrdinalIgnoreCase);
            var authority = alreadyReferenceMatched
                ? entity.SourceAuthority
                : entity.SourceAuthority + "; SCUnpacked cross-build UUID+world-position matched identity/QT metadata only";
            var status = alreadyReferenceMatched ? entity.DataStatus : "LOCAL_DIRECT_GEOMETRY_REFERENCE_IDENTITY";

            dataset.Entities[i] = entity with
            {
                Name = reference.Name,
                Type = reference.Type,
                ParentSourceUuid = reference.ParentUuid ?? entity.ParentSourceUuid,
                Hidden = reference.Hidden,
                QuantumTravelValid = reference.QtValid,
                SourceAuthority = authority,
                DataStatus = status
            };

            var canonicalId = $"uuid:{entity.SourceUuid.ToLowerInvariant()}";
            for (var n = 0; n < dataset.CanonicalNodes.Count; n++)
            {
                var node = dataset.CanonicalNodes[n];
                if (!node.CanonicalId.Equals(canonicalId, StringComparison.OrdinalIgnoreCase))
                    continue;
                dataset.CanonicalNodes[n] = node with
                {
                    Name = reference.Name,
                    Type = reference.Type,
                    ParentSourceUuid = reference.ParentUuid ?? node.ParentSourceUuid,
                    SourceAuthority = authority,
                    DataStatus = status
                };
                break;
            }

            enriched++;
        }

        dataset.Diagnostics.Add(
            $"Exact-world reference identity enrichment: {enriched} placement(s) matched CURRENT LIVE UUID+world XYZ to SCUnpacked within {toleranceMeters:N3} m; geometry/transforms remain exclusively Data.p4k.");
        return enriched;
    }
}

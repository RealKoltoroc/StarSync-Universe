using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class GalaxySystemBuilder
{
    public static IReadOnlyList<GalaxySystemRecord> Build(
        IReadOnlyDictionary<string, UniverseDataset> datasets,
        IReadOnlyList<JumpConnection> jumps)
    {
        return datasets.Values
            .OrderBy(x => x.System, StringComparer.OrdinalIgnoreCase)
            .Select(dataset => new GalaxySystemRecord(
                System: dataset.System,
                FrameId: $"system:{dataset.System.ToLowerInvariant()}",
                GalaxyX: dataset.GalaxyX,
                GalaxyY: dataset.GalaxyY,
                GalaxyZ: dataset.GalaxyZ,
                PositionStatus: dataset.GalaxyPositionStatus,
                ReciprocalJumpDegree: jumps.Count(j => j.ReciprocalEndpointsPresent &&
                    (j.SystemA.Equals(dataset.System, StringComparison.OrdinalIgnoreCase) || j.SystemB.Equals(dataset.System, StringComparison.OrdinalIgnoreCase))),
                SourceAuthority: dataset.GalaxyX.HasValue
                    ? dataset.GalaxyPositionAuthority + "; reciprocal jump degree LOCAL_DERIVED"
                    : "LOCAL_DIRECT system existence + LOCAL_DERIVED reciprocal topology",
                DataStatus: dataset.GalaxyX.HasValue
                    ? "LOCAL_DIRECT_DATACORE_GALACTIC_POSITION_UNIT_SEMANTICS_UNRESOLVED"
                    : "LOCAL_TOPOLOGY_METRIC_POSITION_UNRESOLVED"))
            .ToArray();
    }
}

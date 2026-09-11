using StarSyncUniverse.Data;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class OrbitalInfrastructureDiagnostics
{
    private static readonly string[] StantonMajorStationUuids =
    [
        "ab29f65e-c792-4b1f-b23d-5810cb0ef416", // Everus Harbor
        "45d2d370-9fab-4e6b-b84b-e9c913bc316a", // Seraphim Station
        "164ca676-83eb-448d-ab0b-1ddde8a9c0bd", // Baijini Point
        "233238ee-adeb-4405-8045-49fa07370f37"  // Port Tressler
    ];

    public static IReadOnlyList<string> Run(UniverseDataset dataset, ScUnpackedCatalog catalog)
    {
        var messages = new List<string>();
        var promoted = dataset.Entities
            .Where(e => e.DataStatus.Equals("LOCAL_DIRECT_GEOMETRY_REFERENCE_IDENTITY", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (promoted.Length == 0)
        {
            messages.Add("Orbital infrastructure proof: no promoted body-orbital infrastructure placements found.");
            return messages;
        }

        var compared = 0;
        var maxDelta = 0d;
        foreach (var entity in promoted)
        {
            var reference = catalog.FirstByUuid(entity.SourceUuid);
            if (reference is null || !reference.System.Equals(dataset.System, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(reference.ParentUuid, entity.ParentSourceUuid, StringComparison.OrdinalIgnoreCase))
                continue;

            var dx = entity.X - reference.X;
            var dy = entity.Y - reference.Y;
            var dz = entity.Z - reference.Z;
            var delta = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (!double.IsFinite(delta))
                continue;
            compared++;
            maxDelta = Math.Max(maxDelta, delta);
        }

        messages.Add(
            $"Orbital infrastructure transform proof: promoted={promoted.Length}; cross-build UUID+parent reference comparisons={compared}; max world-XYZ delta={maxDelta:G12} m. CURRENT LIVE Data.p4k remains geometry authority.");

        var lagrangePromoted = promoted.Where(e => e.Id.StartsWith("lagrange-infra:", StringComparison.OrdinalIgnoreCase)).ToArray();
        var lagrangeCompared = 0;
        var lagrangeMaxDelta = 0d;
        foreach (var entity in lagrangePromoted)
        {
            var reference = catalog.FirstByUuid(entity.SourceUuid);
            if (reference is null || !reference.System.Equals(dataset.System, StringComparison.OrdinalIgnoreCase)) continue;
            var dx = entity.X - reference.X;
            var dy = entity.Y - reference.Y;
            var dz = entity.Z - reference.Z;
            var delta = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (!double.IsFinite(delta)) continue;
            lagrangeCompared++;
            lagrangeMaxDelta = Math.Max(lagrangeMaxDelta, delta);
        }
        messages.Add($"Lagrange infrastructure transform proof: promoted={lagrangePromoted.Length}; cross-build UUID+world-position reference comparisons={lagrangeCompared}; max world-XYZ delta={lagrangeMaxDelta:G12} m; identity-only reference, geometry LOCAL_DIRECT.");

        if (dataset.System.Equals("stanton", StringComparison.OrdinalIgnoreCase))
        {
            var majors = promoted
                .Where(e => !string.IsNullOrWhiteSpace(e.SourceUuid) && StantonMajorStationUuids.Contains(e.SourceUuid!, StringComparer.OrdinalIgnoreCase))
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            messages.Add(
                $"Stanton major orbital station proof: {majors.Length}/4 resolved from CURRENT LIVE body SOC placements: {string.Join(", ", majors.Select(e => e.Name))}.");
        }

        var orbitCount = dataset.SimulatedOrbits.Count;
        var stationOrbitCount = dataset.SimulatedOrbits.Count(o =>
            o.Category.Equals("Station", StringComparison.OrdinalIgnoreCase) ||
            o.Category.Equals("SecurityStation", StringComparison.OrdinalIgnoreCase) ||
            o.Category.Equals("ShippingHub", StringComparison.OrdinalIgnoreCase));
        messages.Add(
            $"Simulated orbit proof: total={orbitCount}; station-class={stationOrbitCount}; model=CIRCULAR_CURRENT_RADIUS_MINIMUM_INCLINATION_TO_PARENT_AXIS; visualization only, not dynamic game truth.");
        return messages;
    }
}

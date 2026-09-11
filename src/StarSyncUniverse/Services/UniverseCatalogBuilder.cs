using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class UniverseCatalogBuilder
{
    public static IReadOnlyList<UniverseCatalogEntry> Build(UniverseDataset dataset) =>
        dataset.Entities
            .Select(e => new UniverseCatalogEntry(
                dataset.System,
                e.Id,
                e.SourceUuid,
                e.Name,
                Classify(e),
                e.Type,
                e.EntityClass,
                e.ParentId,
                e.ParentSourceUuid,
                e.X,
                e.Y,
                e.Z,
                e.Hidden,
                e.QuantumTravelValid,
                e.SourcePath,
                e.SourceAuthority,
                e.DataStatus))
            .OrderBy(e => e.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static string Classify(UniverseEntity e)
    {
        var name = e.Name ?? string.Empty;
        var path = e.SourcePath ?? string.Empty;
        var type = e.Type ?? string.Empty;

        if (type.Equals("Star", StringComparison.OrdinalIgnoreCase)) return "Star";
        if (type.Equals("Planet", StringComparison.OrdinalIgnoreCase)) return "Planet";
        if (type.Equals("Moon", StringComparison.OrdinalIgnoreCase)) return "Moon";
        if (name.StartsWith("Transit Point ", StringComparison.OrdinalIgnoreCase) || name.Contains(" Nav Point", StringComparison.OrdinalIgnoreCase) || name.Contains("Navigation Beacon", StringComparison.OrdinalIgnoreCase)) return "NavPoint";
        if (name.Contains("Gateway", StringComparison.OrdinalIgnoreCase) || path.Contains("gateway", StringComparison.OrdinalIgnoreCase)) return "Gateway";
        if (name.StartsWith("People's Service Station", StringComparison.OrdinalIgnoreCase)) return "Station";
        // Levski is a CURRENT-LIVE hierarchical LocationObjectContainer nested inside the
        // Glaciem/Delamar host placement. Its source path does not contain the generic
        // '/station/' token, so classify the actual child container explicitly as a station.
        if (name.Equals("Levski", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/loc/flagship/nyx/levski/", StringComparison.OrdinalIgnoreCase)) return "Station";
        if (name.StartsWith("QV Breaker Station", StringComparison.OrdinalIgnoreCase)) return "BreakerStation";
        if (name.StartsWith("QV Extraction Station", StringComparison.OrdinalIgnoreCase)) return "ExtractionStation";
        if (name.StartsWith("QV Logistics Station", StringComparison.OrdinalIgnoreCase)) return "LogisticsStation";
        if (type.Equals("Outpost", StringComparison.OrdinalIgnoreCase)) return "Outpost";
        if (path.Contains("cluster_modular", StringComparison.OrdinalIgnoreCase) || path.Contains("asteroidcluster", StringComparison.OrdinalIgnoreCase)) return "AsteroidCluster";
        if (path.Contains("asteroidfield", StringComparison.OrdinalIgnoreCase)) return "AsteroidField";
        if (path.Contains("gascloud", StringComparison.OrdinalIgnoreCase)) return "GasCloud";
        if (path.Contains("jumppoint", StringComparison.OrdinalIgnoreCase) || name.Contains("Jump Point", StringComparison.OrdinalIgnoreCase)) return "JumpPoint";
        if (path.Contains("lagrangepoints", StringComparison.OrdinalIgnoreCase) || name.Contains(" L1", StringComparison.OrdinalIgnoreCase) || name.Contains(" L2", StringComparison.OrdinalIgnoreCase) || name.Contains(" L3", StringComparison.OrdinalIgnoreCase) || name.Contains(" L4", StringComparison.OrdinalIgnoreCase) || name.Contains(" L5", StringComparison.OrdinalIgnoreCase)) return "LagrangePoint";
        if (path.Contains("glaciemring", StringComparison.OrdinalIgnoreCase)) return "RingSegment";
        if (path.Contains("station/comm", StringComparison.OrdinalIgnoreCase) || name.Contains("Comm Array", StringComparison.OrdinalIgnoreCase)) return "CommArray";
        if (path.Contains("station/security", StringComparison.OrdinalIgnoreCase)) return "SecurityStation";
        if (path.Contains("station/shippinghub", StringComparison.OrdinalIgnoreCase)) return "ShippingHub";
        if (path.Contains("station", StringComparison.OrdinalIgnoreCase) || name.Contains("Station", StringComparison.OrdinalIgnoreCase) || name.Contains("Gateway", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Port ", StringComparison.OrdinalIgnoreCase)) return "Station";
        if (type.Equals("LandingZone", StringComparison.OrdinalIgnoreCase)) return "LandingZone";
        if (type.Equals("NavPoint", StringComparison.OrdinalIgnoreCase)) return "NavPoint";
        if (type.Equals("Anomaly", StringComparison.OrdinalIgnoreCase)) return "Anomaly";
        if (type.Contains("Manmade", StringComparison.OrdinalIgnoreCase)) return "Manmade";
        if (type.Equals("Asteroid_ValidQT", StringComparison.OrdinalIgnoreCase) ||
            (e.QuantumTravelValid && type.Contains("Asteroid", StringComparison.OrdinalIgnoreCase))) return "AsteroidCluster";
        if (type.Contains("Asteroid", StringComparison.OrdinalIgnoreCase)) return "Asteroid";
        return "Other";
    }
}

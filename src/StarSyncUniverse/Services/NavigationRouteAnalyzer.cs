using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

/// <summary>
/// Derives route-level operational metadata without changing spatial geometry.
/// Interstellar transit fuel information is a gameplay-cost policy input and is
/// intentionally kept separate from metric distance and source-authoritative universe data.
/// </summary>
public static class NavigationRouteAnalyzer
{
    public const double InterstellarTransitQuantumFuelFraction = 0.25d;

    public static NavigationRouteSummary Analyze(NavigationRouteResult route)
    {
        var interstellarTransitCount = route.Legs.Count(leg => leg.LegType.Equals("JUMP", StringComparison.OrdinalIgnoreCase));
        var inSystemSegmentCount = route.Legs.Count - interstellarTransitCount;

        return new NavigationRouteSummary(
            SegmentCount: route.Legs.Count,
            InSystemSegmentCount: inSystemSegmentCount,
            InterstellarTransitCount: interstellarTransitCount,
            MeasurableInSystemDistanceMeters: route.MeasurableInSystemDistanceMeters,
            PerInterstellarTransitQuantumFuelFraction: interstellarTransitCount > 0 ? InterstellarTransitQuantumFuelFraction : null,
            QuantumFuelAggregationStatus: interstellarTransitCount > 0
                ? "PER_TRANSIT_RULE_ONLY_AGGREGATION_NOT_ASSUMED"
                : "NOT_APPLICABLE",
            DataStatus: interstellarTransitCount > 0
                ? "LOCAL_ROUTE_PLUS_USER_SUPPLIED_GAMEPLAY_COST_RULE"
                : "LOCAL_ROUTE_NO_INTERSTELLAR_TRANSIT");
    }
}

public sealed record NavigationRouteSummary(
    int SegmentCount,
    int InSystemSegmentCount,
    int InterstellarTransitCount,
    double MeasurableInSystemDistanceMeters,
    double? PerInterstellarTransitQuantumFuelFraction,
    string QuantumFuelAggregationStatus,
    string DataStatus);

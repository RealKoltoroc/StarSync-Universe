using System.Text;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class UniverseCoverageReportWriter
{
    public static async Task<string> WriteAsync(
        IReadOnlyDictionary<string, UniverseDataset> datasets,
        IReadOnlyList<JumpConnection> jumps,
        IReadOnlyList<SystemRouteResult> routeProofs,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StarSyncUniverse", "Snapshots");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "coverage.current.txt");

        var sb = new StringBuilder();
        sb.AppendLine("StarSyncUniverse coverage report");
        sb.AppendLine($"Generated UTC: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine();

        foreach (var dataset in datasets.Values.OrderBy(x => x.System, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"[{dataset.System.ToUpperInvariant()}]");
            sb.AppendLine($"Build: {dataset.Build}");
            sb.AppendLine($"Entities: {dataset.Entities.Count}");
            sb.AppendLine($"Canonical nodes: {dataset.CanonicalNodes.Count}");
            sb.AppendLine($"Placements: {dataset.Placements.Count}");
            sb.AppendLine($"Frames: {dataset.Frames.Count}");
            sb.AppendLine($"Bodies: {dataset.Bodies.Count}");
            sb.AppendLine($"Simulated visualization orbits: {dataset.SimulatedOrbits.Count}");
            sb.AppendLine($"Temporal transforms: {dataset.TemporalTransforms.Count}");
            sb.AppendLine($"Body-local anchors: {dataset.BodyAnchors.Count}");
            sb.AppendLine($"Near-surface anchors: {dataset.BodyAnchors.Count(a => a.AnchorScope == "NEAR_SURFACE")}");
            sb.AppendLine($"Spatial regions: {dataset.Regions.Count}");
            sb.AppendLine($"Spatial volumes: {dataset.Volumes.Count}");
            sb.AppendLine($"Validation: {dataset.Diagnostics.FirstOrDefault(x => x.StartsWith("Validation:", StringComparison.Ordinal)) ?? "not run"}");
            sb.AppendLine("Provenance:");
            foreach (var p in dataset.Provenance)
                sb.AppendLine($"  - {p.SourceType}: {p.Path} | {p.FingerprintKind}={p.Fingerprint} | {p.DataStatus}");
            sb.AppendLine("Proof diagnostics:");
            foreach (var d in dataset.Diagnostics.Where(x =>
                x.StartsWith("G1 ") || x.StartsWith("G2 ") || x.StartsWith("G3 ") ||
                x.StartsWith("G4 ") || x.StartsWith("G5 ") || x.StartsWith("G6 ") ||
                x.StartsWith("G7 ") || x.StartsWith("G8 ") || x.StartsWith("G9 ") ||
                x.StartsWith("Spatial index") ||
                x.StartsWith("Orbital infrastructure proof") ||
                x.StartsWith("Orbital infrastructure transform proof") ||
                x.StartsWith("Stanton major orbital station proof") ||
                x.StartsWith("Simulated orbit proof")))
                sb.AppendLine("  - " + d);
            sb.AppendLine();
        }

        sb.AppendLine("[JUMP GRAPH]");
        foreach (var jump in jumps)
            sb.AppendLine($"- {jump.ConnectionId}: {jump.SystemA}<->{jump.SystemB} reciprocal={jump.ReciprocalEndpointsPresent} status={jump.DataStatus}");
        sb.AppendLine();

        sb.AppendLine("[ROUTE PROOFS]");
        foreach (var route in routeProofs)
            sb.AppendLine($"- {route.StartSystem}->{route.DestinationSystem}: found={route.Found}; path={string.Join(" -> ", route.Systems)}; status={route.DataStatus}");
        sb.AppendLine();

        sb.AppendLine("[INTENTIONALLY UNRESOLVED / NOT INVENTED]");
        sb.AppendLine("- Absolute planetary spin phase / epoch / alignment");
        sb.AppendLine("- Quaternion handedness and active/passive orientation semantics");
        sb.AppendLine("- Dynamic planetary/moon orbit parameters where current local non-zero instance values are not proven");
        sb.AppendLine("- Common metric galaxy coordinate frame between Stanton/Pyro/Nyx");
        sb.AppendLine("- Geographic latitude/longitude axis and zero-meridian convention");
        sb.AppendLine("- Exact live spawn coordinates for probabilistic asteroid/resource distributions");

        await File.WriteAllTextAsync(path, sb.ToString(), cancellationToken);
        return path;
    }
}

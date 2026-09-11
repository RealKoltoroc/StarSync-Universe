using StarSyncUniverse.Data;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public sealed class UniverseImportCoordinator
{
    private static readonly string[] Systems = ["stanton", "pyro", "nyx"];
    private readonly StarBreakerClient _starBreaker;
    private readonly ScUnpackedCatalog _catalog;

    public UniverseImportCoordinator(StarBreakerClient starBreaker, ScUnpackedCatalog catalog)
    {
        _starBreaker = starBreaker;
        _catalog = catalog;
    }

    public async Task<IReadOnlyDictionary<string, UniverseDataset>> ImportAllAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, UniverseDataset>(StringComparer.OrdinalIgnoreCase);
        var systemImporter = new SystemSocImporter(_starBreaker, _catalog);
        var bodyImporter = new BodyPhysicalImporter(_starBreaker);

        foreach (var system in Systems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"Importing {system} system graph from current Data.p4k…");
            var dataset = await systemImporter.ImportAsync(system, cancellationToken);

            progress?.Report($"Importing {system} CURRENT LIVE DataCore solar-system coordinates…");
            try
            {
                var solarSystem = await new SolarSystemDataCoreImporter(_starBreaker).ImportAsync(system, cancellationToken);
                if (solarSystem is not null)
                {
                    dataset.GalaxyX = solarSystem.GalaxyX;
                    dataset.GalaxyY = solarSystem.GalaxyY;
                    dataset.GalaxyZ = solarSystem.GalaxyZ;
                    dataset.GalaxyPositionStatus = solarSystem.DataStatus;
                    dataset.GalaxyPositionAuthority = solarSystem.SourceAuthority;
                    dataset.Diagnostics.Add($"Galaxy/System DataCore position: ({solarSystem.GalaxyX:R}, {solarSystem.GalaxyY:R}, {solarSystem.GalaxyZ:R}) from SSolarSystem.galacticPosition; unit semantics intentionally unresolved.");
                }
            }
            catch (Exception ex)
            {
                dataset.Diagnostics.Add("SSolarSystem DataCore import failed: " + ex.Message);
            }

            progress?.Report($"Importing {system} body radius/rotation parameters…");
            try
            {
                dataset.Bodies.AddRange(await bodyImporter.ImportAsync(system, dataset.Entities, cancellationToken));
            }
            catch (Exception ex)
            {
                dataset.Diagnostics.Add($"Body physical import failed: {ex.Message}");
            }

            progress?.Report($"Importing {system} direct body-orbital infrastructure from current Data.p4k…");
            try
            {
                var orbitalInfrastructure = await new BodyOrbitalInfrastructureImporter(_starBreaker, _catalog).ImportAsync(dataset, cancellationToken);
                dataset.Diagnostics.Add($"Body-orbital infrastructure: {orbitalInfrastructure.Count} direct station/infrastructure placement(s) promoted into the system catalog.");
                var lagrangeInfrastructure = await new LagrangeInfrastructureImporter(_starBreaker, _catalog).ImportAsync(dataset, cancellationToken);
                dataset.Diagnostics.Add($"Lagrange infrastructure: {lagrangeInfrastructure.Count} direct station/rest-stop placement(s) promoted into the system catalog.");
                if (system.Equals("nyx", StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Report("Resolving Nyx hierarchical station/location carriers from current Data.p4k…");
                    var hierarchicalInfrastructure = await new HierarchicalInfrastructureImporter(_starBreaker, _catalog).ImportAsync(dataset, cancellationToken);
                    dataset.Diagnostics.Add($"Hierarchical infrastructure: {hierarchicalInfrastructure.Count} CURRENT LIVE nested location/station placement(s) promoted into the Nyx catalog.");
                }

                progress?.Report($"Resolving {system} jump-point gateway infrastructure from current Data.p4k…");
                var jumpPointInfrastructure = await new JumpPointInfrastructureImporter(_starBreaker, _catalog).ImportAsync(dataset, cancellationToken);
                dataset.Diagnostics.Add($"Jump-point gateway infrastructure: {jumpPointInfrastructure.Count} identity-bearing CURRENT LIVE station/gateway placement(s) promoted into the system catalog.");

                dataset.SimulatedOrbits.AddRange(SimulatedOrbitBuilder.Build(dataset));
                dataset.Diagnostics.Add($"Simulated orbit layer: {dataset.SimulatedOrbits.Count} circular visualization orbit(s) derived from current child/parent radius and parent rotation axis; no dynamic orbital truth claimed.");
            }
            catch (Exception ex)
            {
                dataset.Diagnostics.Add("Body-orbital infrastructure import failed: " + ex.Message);
            }

            if (system.Equals("stanton", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report("Importing Aaron Halo spatial region from current Data.p4k…");
                try
                {
                    dataset.Regions.Add(await new AaronHaloImporter(_starBreaker).ImportAsync(cancellationToken));
                }
                catch (Exception ex)
                {
                    dataset.Diagnostics.Add("Aaron Halo import failed: " + ex.Message);
                }
            }

            progress?.Report($"Building {system} body-fixed frames and temporal spin transforms…");
            BodyTemporalTransformBuilder.Build(dataset);

            progress?.Report($"Importing {system} body-local surface anchors…");
            try
            {
                dataset.BodyAnchors.AddRange(await new SurfaceAnchorImporter(_starBreaker, _catalog).ImportAsync(dataset, cancellationToken));
                dataset.Diagnostics.Add($"Body-local anchors: {dataset.BodyAnchors.Count} anchor(s), {dataset.BodyAnchors.Count(a => a.AnchorScope == "NEAR_SURFACE")} near-surface candidate(s) from current body ObjectContainers.");
            }
            catch (Exception ex)
            {
                dataset.Diagnostics.Add("Surface anchor import failed: " + ex.Message);
            }

            if (system.Equals("stanton", StringComparison.OrdinalIgnoreCase))
            {
                var newBabbage = dataset.BodyAnchors.FirstOrDefault(a => a.Name.Equals("New Babbage", StringComparison.OrdinalIgnoreCase));
                if (newBabbage is not null && newBabbage.SourcePath.EndsWith(".socpak", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        progress?.Report("Resolving one-level external ObjectContainer graph for New Babbage proof…");
                        var deep = await new DeepObjectContainerResolver(_starBreaker).ResolveAsync(newBabbage.SourcePath, maxDepth: 1, maxContainers: 8, cancellationToken);
                        dataset.Diagnostics.Add($"G1 deep OC proof New Babbage: containers={deep.ContainersResolved}; raw nodes={deep.Nodes.Count}; unresolved refs={deep.UnresolvedReferences.Count}; transforms intentionally uncomposed [{deep.DataStatus}].");
                    }
                    catch (Exception ex)
                    {
                        dataset.Diagnostics.Add("G1 deep OC proof New Babbage failed: " + ex.Message);
                    }
                }
            }

            dataset.SurfaceCoverage.AddRange(SurfaceCoverageBuilder.Build(dataset));
            dataset.OrientationEvidence.Add(OrientationEvidenceBuilder.Build(dataset));
            dataset.Diagnostics.Add($"Surface coverage summary: {dataset.SurfaceCoverage.Count} body record(s), {dataset.SurfaceCoverage.Count(x => x.NearSurfaceCount > 0)} with near-surface anchors.");
            try
            {
                var phaseEvidence = await new RotationPhaseEvidenceBuilder(_starBreaker).BuildAsync(dataset, cancellationToken);
                dataset.RotationPhaseEvidence.Add(phaseEvidence);
                dataset.Diagnostics.Add($"Rotation phase discovery: scanned {phaseEvidence.ContainersScanned} body root XML container(s); candidate fields={phaseEvidence.AbsolutePhaseCandidateFields.Count}; status={phaseEvidence.DataStatus}.");
            }
            catch (Exception ex)
            {
                dataset.Diagnostics.Add("Rotation phase evidence scan failed: " + ex.Message);
            }

            progress?.Report($"Resolving {system} asteroid-cluster volume templates…");
            try
            {
                dataset.Volumes.AddRange(await new AsteroidClusterVolumeImporter(_starBreaker).ImportAsync(dataset, cancellationToken));
            }
            catch (Exception ex)
            {
                dataset.Diagnostics.Add("Asteroid cluster volume import failed: " + ex.Message);
            }

            progress?.Report($"Resolving {system} placed gas-cloud/ring-segment bounds…");
            try
            {
                var placedVolumes = await new PlacedSpatialVolumeImporter(_starBreaker).ImportAsync(dataset, cancellationToken);
                var existingVolumeIds = dataset.Volumes.Select(v => v.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
                dataset.Volumes.AddRange(placedVolumes.Where(v => existingVolumeIds.Add(v.Id)));

                var glaciem = GlaciemRingRegionBuilder.Build(dataset);
                if (glaciem is not null && !dataset.Regions.Any(r => r.Id.Equals(glaciem.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    dataset.Regions.Add(glaciem);
                    dataset.Diagnostics.Add($"Glaciem ring envelope: inner={glaciem.InnerRadiusMeters:N0} m; outer={glaciem.OuterRadiusMeters:N0} m; thickness={glaciem.ThicknessMeters:N0} m; derived only from CURRENT LIVE segment placements/bounds.");
                }
            }
            catch (Exception ex)
            {
                dataset.Diagnostics.Add("Placed spatial volume import failed: " + ex.Message);
            }

            progress?.Report($"Applying strict UUID+world reference identity matches for {system}…");
            new ExactWorldReferenceIdentityEnricher(_catalog).Apply(dataset);

            dataset.AdvancedLayerAvailability.AddRange(AdvancedLayerAvailabilityBuilder.Build(dataset, _catalog));
            foreach (var layer in dataset.AdvancedLayerAvailability)
                dataset.Diagnostics.Add($"Advanced layer {layer.Layer}: {layer.DataStatus}; authoritativeImport={layer.AuthoritativeImportEnabled}; {layer.Reason}");

            foreach (var diagnostic in TransformFoundationDiagnostics.Run(dataset))
                dataset.Diagnostics.Add(diagnostic);
            foreach (var proof in KnownProofDiagnostics.Run(dataset))
                dataset.Diagnostics.Add(proof);
            foreach (var spatialProof in SpatialIndexDiagnostics.Run(dataset))
                dataset.Diagnostics.Add(spatialProof);

            CompareWithScUnpacked(dataset);
            foreach (var orbitalProof in OrbitalInfrastructureDiagnostics.Run(dataset, _catalog))
                dataset.Diagnostics.Add(orbitalProof);
            var issues = UniverseValidator.Validate(dataset);
            foreach (var issue in issues.Take(50))
                dataset.Diagnostics.Add(issue);
            if (issues.Count > 50)
                dataset.Diagnostics.Add($"Validation issues truncated in diagnostics: {issues.Count - 50} more.");

            progress?.Report($"Writing {system} snapshot…");
            var snapshotPath = await UniverseSnapshotWriter.WriteAsync(dataset, cancellationToken);
            dataset.Diagnostics.Add("Snapshot: " + snapshotPath);
            result[system] = dataset;
        }

        progress?.Report("Building reciprocal jump graph, route proofs and global universe index…");
        var jumps = JumpGraphBuilder.Build(result);
        var routeProofs = new[]
        {
            InterSystemRoutePlanner.FindRoute(jumps, "stanton", "nyx"),
            InterSystemRoutePlanner.FindRoute(jumps, "stanton", "terra")
        };
        foreach (var diagnostic in PostImportIntegrationDiagnostics.Run(result, jumps))
            foreach (var dataset in result.Values)
                dataset.Diagnostics.Add(diagnostic);

        var indexPath = await UniverseIndexWriter.WriteAsync(result, jumps, routeProofs, cancellationToken);
        var coveragePath = await UniverseCoverageReportWriter.WriteAsync(result, jumps, routeProofs, cancellationToken);
        foreach (var dataset in result.Values)
        {
            dataset.Diagnostics.Add($"Jump graph: {jumps.Count} connection(s), {jumps.Count(j => j.ReciprocalEndpointsPresent)} reciprocal pair(s).");
            foreach (var route in routeProofs)
                dataset.Diagnostics.Add($"Route proof {route.StartSystem}->{route.DestinationSystem}: {(route.Found ? string.Join(" -> ", route.Systems) : "NO RECIPROCAL ROUTE")} [{route.DataStatus}]");
            dataset.Diagnostics.Add("Universe index: " + indexPath);
            dataset.Diagnostics.Add("Coverage report: " + coveragePath);
        }

        progress?.Report("Refreshing final per-system snapshots with global G4-G9 diagnostics…");
        foreach (var dataset in result.Values)
            await UniverseSnapshotWriter.WriteAsync(dataset, cancellationToken);

        return result;
    }

    private void CompareWithScUnpacked(UniverseDataset dataset)
    {
        var compared = 0;
        var maxDelta = 0d;
        var mismatchesOverOneMeter = 0;

        foreach (var entity in dataset.Entities)
        {
            var refEntry = _catalog.FirstByUuid(entity.SourceUuid);
            if (refEntry is null || !refEntry.System.Equals(dataset.System, StringComparison.OrdinalIgnoreCase))
                continue;

            var delta = Math.Sqrt(
                Math.Pow(entity.X - refEntry.X, 2) +
                Math.Pow(entity.Y - refEntry.Y, 2) +
                Math.Pow(entity.Z - refEntry.Z, 2));
            compared++;
            maxDelta = Math.Max(maxDelta, delta);
            if (delta > 1d) mismatchesOverOneMeter++;
        }

        var compatible = _catalog.IsBuildCompatible(dataset.Build);
        dataset.Diagnostics.Add(
            $"SCUnpacked {(compatible ? "same-build validation" : "cross-build reference comparison")}: {compared} placements compared; max delta {maxDelta:N3} m; >1 m mismatches {mismatchesOverOneMeter}. " +
            (compatible
                ? "Data.p4k remains authoritative."
                : $"Catalog P4={_catalog.SourceP4?.ToString() ?? "unknown"} differs from CURRENT LIVE and cannot be used as an authority/enrichment source."));
    }
}

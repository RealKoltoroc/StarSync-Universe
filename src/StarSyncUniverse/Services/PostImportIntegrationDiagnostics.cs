using StarSyncUniverse.Domain;
using StarSyncUniverse.Transforms;
using StarSyncUniverse.Renderer;

namespace StarSyncUniverse.Services;

public static class PostImportIntegrationDiagnostics
{
    public static IReadOnlyList<string> Run(IReadOnlyDictionary<string, UniverseDataset> datasets, IReadOnlyList<JumpConnection> jumps)
    {
        var messages = new List<string>();
        var service = new UniverseService(datasets, jumps);

        if (datasets.TryGetValue("stanton", out var stanton) && datasets.TryGetValue("nyx", out var nyx))
        {
            var start = stanton.Entities.FirstOrDefault(e => e.Name.Equals("Hurston", StringComparison.OrdinalIgnoreCase)) ?? stanton.Entities.First();
            var destination = nyx.Entities.FirstOrDefault(e => e.Type.Equals("Planet", StringComparison.OrdinalIgnoreCase)) ?? nyx.Entities.First();
            var route = service.FindRoute(
                new NavigationTarget("stanton", start.Name, start.Id, null, null, null, "PLACEMENT_SNAPSHOT_XYZ", start.DataStatus),
                new NavigationTarget("nyx", destination.Name, destination.Id, null, null, null, "PLACEMENT_SNAPSHOT_XYZ", destination.DataStatus));
            messages.Add($"G6 navigation proof: {start.Name}-> {destination.Name}; found={route.Found}; legs={route.Legs.Count}; measurable local distance={route.MeasurableInSystemDistanceMeters:N0} m; status={route.DataStatus}.");
            ValidateLocalRouteLegs(route, datasets);
        }

        if (datasets.TryGetValue("pyro", out var pyro) && datasets.TryGetValue("stanton", out stanton))
        {
            var terminus = pyro.Entities.FirstOrDefault(e => e.Name.Equals("Terminus", StringComparison.OrdinalIgnoreCase));
            var baijini = stanton.Entities.FirstOrDefault(e => e.Name.Equals("Baijini Point", StringComparison.OrdinalIgnoreCase));
            if (terminus is null || baijini is null)
                throw new InvalidOperationException("Requested Pyro Terminus -> Stanton Baijini Point navigation proof targets are missing.");

            var route = service.FindRoute(
                new NavigationTarget("pyro", terminus.Name, terminus.Id, null, null, null, "PLACEMENT_SNAPSHOT_XYZ", terminus.DataStatus),
                new NavigationTarget("stanton", baijini.Name, baijini.Id, null, null, null, "PLACEMENT_SNAPSHOT_XYZ", baijini.DataStatus));
            if (!route.Found || route.Legs.Count < 3 || route.Legs.Count(x => x.LegType.Equals("JUMP", StringComparison.OrdinalIgnoreCase)) != 1)
                throw new InvalidOperationException("Pyro Terminus -> Stanton Baijini Point route did not produce the expected reciprocal jump segmentation.");
            if (route.Legs.Where(x => x.LegType.Equals("JUMP", StringComparison.OrdinalIgnoreCase)).Any(x => x.DistanceMeters.HasValue))
                throw new InvalidOperationException("Inter-system jump leg incorrectly acquired a fabricated metric distance.");
            ValidateLocalRouteLegs(route, datasets);
            var summary = NavigationRouteAnalyzer.Analyze(route);
            if (summary.SegmentCount != route.Legs.Count || summary.InterstellarTransitCount != 1 || summary.PerInterstellarTransitQuantumFuelFraction != 0.25d)
                throw new InvalidOperationException("G6 route summary/cost-policy proof failed for the Pyro -> Stanton transit.");
            var sequence = string.Join(" -> ", route.Legs.Select(l => l.LegType.Equals("JUMP", StringComparison.OrdinalIgnoreCase)
                ? $"INTERSTELLAR_TRANSIT[{l.FromLabel}=>{l.ToLabel}]"
                : $"{l.System}:{l.FromLabel}=>{l.ToLabel}"));
            messages.Add($"G6 requested cross-system proof: Terminus(Pyro)->Baijini Point(Stanton); found=True; segments={summary.SegmentCount}; interstellar transits={summary.InterstellarTransitCount}; measurable local distance={summary.MeasurableInSystemDistanceMeters:N0} m; sequence={sequence}; transit metric distance=UNDEFINED/PASS; user-supplied QT fuel rule={summary.PerInterstellarTransitQuantumFuelFraction:P0} per transit (aggregation intentionally not assumed).");
        }

        if (datasets.TryGetValue("stanton", out stanton))
        {
            var newBabbage = stanton.BodyAnchors.FirstOrDefault(a => a.Name.Equals("New Babbage", StringComparison.OrdinalIgnoreCase));
            if (newBabbage is not null)
            {
                var bookmark = BookmarkFactory.FromSurfaceAnchor(newBabbage, "New Babbage proof bookmark", ["proof", "surface"]);
                var source = new Vector3D(newBabbage.BodyLocalX, newBabbage.BodyLocalY, newBabbage.BodyLocalZ);
                var projected = service.ProjectBodyFixedRelative("stanton", newBabbage.BodyName, source, TimeSpan.Zero);
                var roundTrip = service.ProjectSystemToBodyFixedRelative("stanton", newBabbage.BodyName, projected, TimeSpan.Zero);
                messages.Add($"G7 bookmark proof: kind={bookmark.CoordinateKind}; frame={bookmark.ReferenceFrameId}; round-trip delta={(roundTrip - source).Length:G12} m; no persistent user bookmark written.");

                var remoteBookmark = bookmark with { Revision = bookmark.Revision + 1, UpdatedUtc = bookmark.UpdatedUtc.AddSeconds(1) };
                var localOverlay = new UniverseOverlayEnvelope("1", stanton.Build, 1, [bookmark], [], [], [], [], DateTimeOffset.UtcNow);
                var remoteOverlay = new UniverseOverlayEnvelope("1", stanton.Build, 2, [remoteBookmark], [], [], [], [], DateTimeOffset.UtcNow);
                var merged = OverlayMergeService.Merge(localOverlay, remoteOverlay);
                if (merged.Conflicts.Count == 0 || !merged.Conflicts.Any(x => x.EntityKind == "Bookmark" && x.Id == bookmark.Id && x.Winner == "REMOTE"))
                    throw new InvalidOperationException("G8 conflict telemetry proof failed: higher-revision remote bookmark resolution was not reported.");

                var thirdBookmark = remoteBookmark with { Revision = remoteBookmark.Revision + 1, UpdatedUtc = remoteBookmark.UpdatedUtc.AddSeconds(1), Note = "third-client" };
                var thirdOverlay = new UniverseOverlayEnvelope("1", stanton.Build, 3, [thirdBookmark], [], [], [], [], DateTimeOffset.UtcNow);
                var convergeAbc = OverlayMergeService.Merge(OverlayMergeService.Merge(localOverlay, remoteOverlay).Merged, thirdOverlay).Merged;
                var convergeAcb = OverlayMergeService.Merge(OverlayMergeService.Merge(localOverlay, thirdOverlay).Merged, remoteOverlay).Merged;
                var winnerAbc = convergeAbc.Bookmarks.Single(x => x.Id == bookmark.Id);
                var winnerAcb = convergeAcb.Bookmarks.Single(x => x.Id == bookmark.Id);
                if (winnerAbc.Revision != winnerAcb.Revision || winnerAbc.UpdatedUtc != winnerAcb.UpdatedUtc || winnerAbc.Note != winnerAcb.Note)
                    throw new InvalidOperationException("G8 three-client convergence proof failed: merge order changed the winning bookmark record.");

                var deleteOverlay = new UniverseOverlayEnvelope(
                    "1", stanton.Build, 3,
                    [], [], [], [],
                    [new OverlayTombstoneRecord(bookmark.Id, "Bookmark", remoteBookmark.Revision + 1, remoteBookmark.UpdatedUtc.AddSeconds(1))],
                    DateTimeOffset.UtcNow);
                var deletedMerge = OverlayMergeService.Merge(merged.Merged, deleteOverlay);
                if (deletedMerge.Merged.Bookmarks.Any(x => x.Id == bookmark.Id))
                    throw new InvalidOperationException("G8 tombstone proof failed: deleted bookmark survived a higher-revision tombstone.");

                var resurrectedBookmark = remoteBookmark with
                {
                    Revision = remoteBookmark.Revision + 2,
                    UpdatedUtc = remoteBookmark.UpdatedUtc.AddSeconds(2)
                };
                var resurrectOverlay = new UniverseOverlayEnvelope(
                    "1", stanton.Build, 4,
                    [resurrectedBookmark], [], [], [], [],
                    DateTimeOffset.UtcNow);
                var resurrectedMerge = OverlayMergeService.Merge(deletedMerge.Merged, resurrectOverlay);
                if (!resurrectedMerge.Merged.Bookmarks.Any(x => x.Id == bookmark.Id && x.Revision == resurrectedBookmark.Revision))
                    throw new InvalidOperationException("G8 tombstone proof failed: a newer record could not supersede an older tombstone.");

                messages.Add($"G8 overlay merge proof: baseMergeBookmarks={merged.Merged.Bookmarks.Count}; conflicts={merged.Conflicts.Count}; threeClientConvergence=PASS; tombstoneDelete=PASS; newerRecordResurrection=PASS; status={resurrectedMerge.DataStatus}; no SyncHost write performed.");
            }
        }

        if (datasets.TryGetValue("stanton", out stanton))
        {
            var originEntity = stanton.Entities.FirstOrDefault(e => e.Name.Equals("microTech", StringComparison.OrdinalIgnoreCase)) ?? stanton.Entities.First();
            var render = RenderFrameSnapshotBuilder.Build(stanton, new Vector3D(originEntity.X, originEntity.Y, originEntity.Z), cullRadiusMeters: 2_000_000_000d, includeHidden: false);
            messages.Add($"G5 render proof: floating origin={originEntity.Name}; visible points={render.Points.Count}; culled={render.CulledCount}; coordinate mode={render.CoordinatePrecision}.");

            var fallbackResolver = new MapVisualAssetResolver(Path.Combine(Path.GetTempPath(), "starsync-universe-visual-proof-nonexistent"));
            var fallbackVisual = fallbackResolver.Resolve("proof:station", MapVisualClass.Station, prefer3D: true);
            if (!fallbackVisual.AssetPath.Equals("builtin://map-visual/Station", StringComparison.Ordinal) || !fallbackVisual.Exists)
                throw new InvalidOperationException("G5 visual asset fallback contract failed.");

            var overrideRoot = Path.Combine(Path.GetTempPath(), "starsync-universe-visual-proof-" + Guid.NewGuid().ToString("N"));
            var overrideDirectory = Path.Combine(overrideRoot, "Overrides", "by-canonical-id");
            Directory.CreateDirectory(overrideDirectory);
            var overrideFile = Path.Combine(overrideDirectory, "proof_station.glb");
            File.WriteAllBytes(overrideFile, [0x67, 0x6c, 0x54, 0x46]);
            try
            {
                var overrideVisual = new MapVisualAssetResolver(overrideRoot).Resolve("proof:station", MapVisualClass.Station, prefer3D: true);
                if (!overrideVisual.IsOverride || overrideVisual.Representation != MapVisualRepresentation.OverrideModel3D || !Path.GetFullPath(overrideVisual.AssetPath).Equals(Path.GetFullPath(overrideFile), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("G5 canonical visual override resolution failed.");
            }
            finally
            {
                Directory.Delete(overrideRoot, recursive: true);
            }

            var surfaceOrigin = SurfaceMapProjectionService.ProjectEquirectangular(0d, 0d, "PROOF", "PROOF");
            if (Math.Abs(surfaceOrigin.X01 - 0.5d) > 1e-12 || Math.Abs(surfaceOrigin.Y01 - 0.5d) > 1e-12)
                throw new InvalidOperationException("G5 surface equirectangular projection proof failed.");
            var terminator = SurfaceMapProjectionService.BuildTerminator(0d, 0d, 64);
            if (terminator.Count != 64)
                throw new InvalidOperationException("G5 terminator geometry proof failed.");
            messages.Add("G5 visual/surface-map proof: deterministic asset fallback=PASS; equirectangular origin=PASS; terminator geometry=PASS; automatic live day/night remains authority-gated.");

            var station = stanton.Entities.FirstOrDefault(e => e.Name.Equals("Port Tressler", StringComparison.OrdinalIgnoreCase));
            if (station is not null)
            {
                var position = service.GetPosition("stanton", station.Id);
                var measurement = service.Measure("stanton", originEntity.Id, station.Id);
                if (!position.CoordinateFrame.Equals(measurement.CoordinateFrame, StringComparison.Ordinal))
                    throw new InvalidOperationException("Spatial position/measurement frame contract mismatch.");
                messages.Add($"Spatial position proof: {position.Name}; xyz=({position.X:N3},{position.Y:N3},{position.Z:N3}) m; frame={position.CoordinateFrame}; status=PASS.");
                messages.Add($"Spatial measurement proof: {measurement.FromName}->{measurement.ToName}; distance={measurement.DistanceMeters:N3} m; delta=({measurement.DeltaX:N3},{measurement.DeltaY:N3},{measurement.DeltaZ:N3}) m; azimuthXY={measurement.AzimuthDegreesSystemXY:F6} deg; elevation={measurement.ElevationAngleDegrees:F6} deg; frame={measurement.CoordinateFrame}.");
            }
        }

        foreach (var pair in datasets.Where(x => x.Value.Volumes.Count > 0))
        {
            var checkedCount = 0;
            foreach (var volume in pair.Value.Volumes)
            {
                var containing = service.FindVolumesContaining(pair.Key, new Vector3D(volume.CenterX, volume.CenterY, volume.CenterZ));
                if (!containing.Any(v => v.Id.Equals(volume.Id, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"Spatial volume index proof failed for '{volume.Id}'.");
                checkedCount++;
            }
            messages.Add($"Spatial volume index proof: system={pair.Key}; center containment={checkedCount}/{pair.Value.Volumes.Count}; status=PASS.");
        }

        if (datasets.TryGetValue("nyx", out nyx))
        {
            var requiredNyx = new[]
            {
                "People's Service Station Alpha",
                "People's Service Station Delta",
                "People's Service Station Theta",
                "People's Service Station Lambda",
                "Levski",
                "Transit Point Glaciem Alpha",
                "Transit Point Glaciem Bravo",
                "Transit Point Glaciem Charlie"
            };
            var missing = requiredNyx.Where(name => !nyx.Entities.Any(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (missing.Length > 0)
                throw new InvalidOperationException("Nyx structural proof missing: " + string.Join(", ", missing));
            var transitQt = nyx.Entities.Count(e => e.Name.StartsWith("Transit Point Glaciem ", StringComparison.OrdinalIgnoreCase) && e.QuantumTravelValid);
            var pss = nyx.Entities.Count(e => e.Name.StartsWith("People's Service Station ", StringComparison.OrdinalIgnoreCase));
            var clinics = nyx.Entities.Count(e => e.Name.StartsWith("PSS ", StringComparison.OrdinalIgnoreCase) && e.Name.EndsWith(" Clinic", StringComparison.OrdinalIgnoreCase));
            var ring = nyx.Regions.FirstOrDefault(r => r.Id.Equals("nyx:glaciem:ring-placement-envelope", StringComparison.OrdinalIgnoreCase));
            messages.Add($"Nyx structural proof: People's Service Stations={pss}; nested clinics={clinics}; Levski=PASS; Glaciem QT transit points={transitQt}/3; Glaciem ring envelope={(ring is null ? "MISSING" : $"{ring.InnerRadiusMeters:N0}-{ring.OuterRadiusMeters:N0} m")}; geometry remains CURRENT LIVE Data.p4k.");
        }

        foreach (var pair in datasets)
        {
            var jumpEntities = pair.Value.Entities
                .Where(e => (e.SourcePath?.Contains("jumppoint", StringComparison.OrdinalIgnoreCase) ?? false) || e.Name.Contains("Jump Point", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var catalog = UniverseCatalogBuilder.Build(pair.Value);
            var classifiedJumpIds = catalog
                .Where(c => c.Category.Equals("JumpPoint", StringComparison.OrdinalIgnoreCase))
                .Select(c => c.PlacementId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var missingClassification = jumpEntities.Where(e => !classifiedJumpIds.Contains(e.Id)).Select(e => e.Name).ToArray();
            if (missingClassification.Length > 0)
                throw new InvalidOperationException($"JumpPoint catalog proof failed for {pair.Key}: {string.Join(", ", missingClassification)}");
            messages.Add($"JumpPoint catalog proof: system={pair.Key}; source jump placements={jumpEntities.Length}; classified JumpPoint={classifiedJumpIds.Count}; hidden source metadata does not suppress operational catalog visibility.");
        }

        var galaxy = GalaxySystemBuilder.Build(datasets, jumps);
        var directGalaxy = galaxy.Count(x => x.GalaxyX.HasValue && x.GalaxyY.HasValue && x.GalaxyZ.HasValue);
        if (directGalaxy != datasets.Count)
            throw new InvalidOperationException($"G4 CURRENT LIVE DataCore position proof incomplete: {directGalaxy}/{datasets.Count} systems resolved.");
        messages.Add($"G4 galaxy-frame proof: systems={galaxy.Count}; CURRENT LIVE SSolarSystem.galacticPosition resolved={directGalaxy}; game-coordinate units intentionally unresolved; reciprocal topology preserved without fabricated metric conversion.");
        return messages;
    }

    private static void ValidateLocalRouteLegs(
        NavigationRouteResult route,
        IReadOnlyDictionary<string, UniverseDataset> datasets)
    {
        foreach (var leg in route.Legs.Where(x => !x.LegType.Equals("JUMP", StringComparison.OrdinalIgnoreCase)))
        {
            if (!datasets.TryGetValue(leg.System, out var dataset))
                throw new InvalidOperationException($"Route leg references unknown system '{leg.System}'.");
            var router = new InSystemVisibilityRouter(dataset);
            var from = new Vector3D(leg.FromX, leg.FromY, leg.FromZ);
            var to = new Vector3D(leg.ToX, leg.ToY, leg.ToZ);
            if (router.IsBlocked(from, to))
                throw new InvalidOperationException($"Route safety proof failed: local leg '{leg.FromLabel}' -> '{leg.ToLabel}' in {leg.System} intersects a proven planet/moon obstacle.");
        }
    }
}

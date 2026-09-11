using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class UniverseValidator
{
    public static IReadOnlyList<string> Validate(UniverseDataset dataset)
    {
        var issues = new List<string>();
        var ids = dataset.Entities.Select(e => e.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in dataset.Entities)
        {
            if (entity.ParentId is not null && !ids.Contains(entity.ParentId))
                issues.Add($"BROKEN_PARENT {entity.Name} ({entity.Id}) -> {entity.ParentId}");

            if (!double.IsFinite(entity.X) || !double.IsFinite(entity.Y) || !double.IsFinite(entity.Z))
                issues.Add($"NON_FINITE_POSITION {entity.Name} ({entity.Id})");

            if (entity.DistanceToParentMeters < 0 || !double.IsFinite(entity.DistanceToParentMeters))
                issues.Add($"INVALID_PARENT_DISTANCE {entity.Name} ({entity.Id})");
        }

        foreach (var body in dataset.Bodies)
        {
            if (body.RadiusMeters <= 0)
                issues.Add($"INVALID_RADIUS {body.Name}: {body.RadiusMeters}");
            if (body.RotationPeriodSeconds < 0)
                issues.Add($"INVALID_ROTATION_PERIOD {body.Name}: {body.RotationPeriodSeconds}");
            var axisLength = Math.Sqrt(body.AxisX * body.AxisX + body.AxisY * body.AxisY + body.AxisZ * body.AxisZ);
            if (axisLength < 0.5)
                issues.Add($"INVALID_ROTATION_AXIS {body.Name}: ({body.AxisX},{body.AxisY},{body.AxisZ})");
        }

        foreach (var region in dataset.Regions)
        {
            if (region.InnerRadiusMeters < 0 || region.OuterRadiusMeters <= region.InnerRadiusMeters)
                issues.Add($"INVALID_REGION_RADII {region.Name}: {region.InnerRadiusMeters}..{region.OuterRadiusMeters}");
            if (region.ThicknessMeters <= 0)
                issues.Add($"INVALID_REGION_THICKNESS {region.Name}: {region.ThicknessMeters}");
        }

        foreach (var volume in dataset.Volumes)
        {
            if (volume.BoundingRadiusMeters <= 0)
                issues.Add($"INVALID_VOLUME_RADIUS {volume.Name}: {volume.BoundingRadiusMeters}");
            if (volume.MaxLocalX <= volume.MinLocalX || volume.MaxLocalY <= volume.MinLocalY || volume.MaxLocalZ <= volume.MinLocalZ)
                issues.Add($"INVALID_VOLUME_BOUNDS {volume.Name}");
        }

        var duplicateIds = dataset.Entities
            .GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();
        foreach (var id in duplicateIds)
            issues.Add($"DUPLICATE_PLACEMENT_ID {id}");

        var canonicalIds = dataset.CanonicalNodes.Select(n => n.CanonicalId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var placementIds = dataset.Placements.Select(p => p.PlacementId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var frameIds = dataset.Frames.Select(f => f.FrameId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var placement in dataset.Placements)
        {
            if (!canonicalIds.Contains(placement.CanonicalId))
                issues.Add($"BROKEN_CANONICAL_REF {placement.PlacementId} -> {placement.CanonicalId}");
            if (placement.ParentPlacementId is not null && !placementIds.Contains(placement.ParentPlacementId))
                issues.Add($"BROKEN_PLACEMENT_PARENT {placement.PlacementId} -> {placement.ParentPlacementId}");
            if (!frameIds.Contains(placement.ParentFrameId))
                issues.Add($"BROKEN_PARENT_FRAME {placement.PlacementId} -> {placement.ParentFrameId}");
        }

        foreach (var frame in dataset.Frames)
        {
            if (frame.ParentFrameId is not null && !frameIds.Contains(frame.ParentFrameId))
                issues.Add($"BROKEN_FRAME_PARENT {frame.FrameId} -> {frame.ParentFrameId}");
            if (frame.CanonicalId is not null && !canonicalIds.Contains(frame.CanonicalId))
                issues.Add($"BROKEN_FRAME_CANONICAL {frame.FrameId} -> {frame.CanonicalId}");
        }

        foreach (var transform in dataset.TemporalTransforms)
        {
            if (!frameIds.Contains(transform.FromFrameId))
                issues.Add($"BROKEN_TEMPORAL_FROM_FRAME {transform.TransformId} -> {transform.FromFrameId}");
            if (!frameIds.Contains(transform.ToFrameId))
                issues.Add($"BROKEN_TEMPORAL_TO_FRAME {transform.TransformId} -> {transform.ToFrameId}");
            if (transform.PeriodSeconds < 0 || !double.IsFinite(transform.PeriodSeconds))
                issues.Add($"INVALID_TEMPORAL_PERIOD {transform.TransformId}: {transform.PeriodSeconds}");
            var axisLength = Math.Sqrt(transform.AxisX * transform.AxisX + transform.AxisY * transform.AxisY + transform.AxisZ * transform.AxisZ);
            if (axisLength < 0.5)
                issues.Add($"INVALID_TEMPORAL_AXIS {transform.TransformId}: ({transform.AxisX},{transform.AxisY},{transform.AxisZ})");
        }

        foreach (var anchor in dataset.BodyAnchors)
        {
            if (!frameIds.Contains(anchor.BodyFixedFrameId))
                issues.Add($"BROKEN_BODY_ANCHOR_FRAME {anchor.Name} -> {anchor.BodyFixedFrameId}");
            if (anchor.ReferenceRadiusMeters <= 0 || !double.IsFinite(anchor.ReferenceRadiusMeters))
                issues.Add($"INVALID_BODY_ANCHOR_REFERENCE_RADIUS {anchor.Name}: {anchor.ReferenceRadiusMeters}");
            if (!double.IsFinite(anchor.BodyLocalX) || !double.IsFinite(anchor.BodyLocalY) || !double.IsFinite(anchor.BodyLocalZ))
                issues.Add($"NON_FINITE_BODY_ANCHOR_POSITION {anchor.Name}");
            if (!double.IsFinite(anchor.AxisLatitudeDegrees) || anchor.AxisLatitudeDegrees < -90 || anchor.AxisLatitudeDegrees > 90)
                issues.Add($"INVALID_BODY_ANCHOR_AXIS_LATITUDE {anchor.Name}: {anchor.AxisLatitudeDegrees}");
            if (!double.IsFinite(anchor.AxisLongitudeDegrees) || anchor.AxisLongitudeDegrees <= -180 || anchor.AxisLongitudeDegrees > 180)
                issues.Add($"INVALID_BODY_ANCHOR_AXIS_LONGITUDE {anchor.Name}: {anchor.AxisLongitudeDegrees}");
        }

        foreach (var evidence in dataset.OrientationEvidence)
        {
            if (evidence.PlacementCount != dataset.Placements.Count)
                issues.Add($"ORIENTATION_EVIDENCE_COUNT_MISMATCH {evidence.System}: {evidence.PlacementCount} != {dataset.Placements.Count}");
            if (!double.IsFinite(evidence.MaxQuaternionNormError) || evidence.MaxQuaternionNormError > 0.01d)
                issues.Add($"INVALID_QUATERNION_NORM_EVIDENCE {evidence.System}: {evidence.MaxQuaternionNormError}");
        }

        foreach (var coverage in dataset.SurfaceCoverage)
        {
            var classified = coverage.NearSurfaceCount + coverage.ElevatedAtmosphericCount + coverage.OrbitalCount + coverage.SubsurfaceCount;
            if (coverage.AnchorCount != classified)
                issues.Add($"SURFACE_COVERAGE_COUNT_MISMATCH {coverage.BodyName}: total={coverage.AnchorCount}, classified={classified}");
        }

        dataset.Diagnostics.Add(issues.Count == 0
            ? "Validation: PASS (no structural integrity errors)."
            : $"Validation: FAIL ({issues.Count} issue(s)).");

        return issues;
    }
}

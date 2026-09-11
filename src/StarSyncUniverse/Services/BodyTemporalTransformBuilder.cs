using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class BodyTemporalTransformBuilder
{
    public static void Build(UniverseDataset dataset)
    {
        foreach (var body in dataset.Bodies)
        {
            var entity = dataset.Entities.FirstOrDefault(e =>
                (!string.IsNullOrWhiteSpace(body.SourceUuid) && string.Equals(e.SourceUuid, body.SourceUuid, StringComparison.OrdinalIgnoreCase)) ||
                (e.SourcePath?.Replace('\\', '/').EndsWith('/' + body.ContainerName + ".socpak", StringComparison.OrdinalIgnoreCase) ?? false));
            if (entity is null) continue;

            var centerFrameId = $"frame:{entity.Id}";
            var fixedFrameId = $"frame:{entity.Id}:bodyfixed";
            if (!dataset.Frames.Any(f => f.FrameId.Equals(fixedFrameId, StringComparison.OrdinalIgnoreCase)))
            {
                var canonical = dataset.Placements.FirstOrDefault(p => p.PlacementId.Equals(entity.Id, StringComparison.OrdinalIgnoreCase))?.CanonicalId;
                dataset.Frames.Add(new SpatialFrameRecord(
                    fixedFrameId,
                    centerFrameId,
                    "BodyFixed",
                    canonical,
                    entity.Id,
                    "Data.p4k/Body ObjectContainer + LOCAL_DERIVED frame",
                    "LOCAL_DERIVED"));
            }

            var transformId = $"spin:{dataset.System}:{entity.Id}";
            if (dataset.TemporalTransforms.Any(t => t.TransformId.Equals(transformId, StringComparison.OrdinalIgnoreCase)))
                continue;

            dataset.TemporalTransforms.Add(new TemporalTransformRecord(
                transformId,
                dataset.System,
                entity.SourceUuid,
                body.Name,
                "BodySpin",
                centerFrameId,
                fixedFrameId,
                body.AxisX,
                body.AxisY,
                body.AxisZ,
                body.RotationPeriodSeconds,
                AlignmentUtc: null,
                AlignmentAngleRadians: null,
                body.SourceAuthority,
                body.RotationPeriodSeconds > 0
                    ? "LOCAL_DIRECT_PERIOD_PHASE_UNRESOLVED"
                    : "LOCAL_DIRECT_STATIC_OR_UNRESOLVED"));
        }
    }
}

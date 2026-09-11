using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class OrientationEvidenceBuilder
{
    public static OrientationConventionEvidence Build(UniverseDataset dataset)
    {
        var placements = dataset.Placements;
        var identity = placements.Count(p =>
            Nearly(p.Q0, 1d) && Nearly(p.Q1, 0d) && Nearly(p.Q2, 0d) && Nearly(p.Q3, 0d));
        var nonTrivial = placements.Count - identity;
        var maxNormError = placements.Count == 0
            ? 0d
            : placements.Max(p => Math.Abs(Math.Sqrt(p.Q0 * p.Q0 + p.Q1 * p.Q1 + p.Q2 * p.Q2 + p.Q3 * p.Q3) - 1d));

        return new OrientationConventionEvidence(
            dataset.System,
            "WXYZ_STRONGLY_SUPPORTED",
            placements.Count,
            identity,
            nonTrivial,
            maxNormError,
            "RIGHT_HANDED_STRONGLY_SUPPORTED",
            "UNRESOLVED",
            "CHILD_TRANSLATION_PARENT_RELATIVE_SYSTEM_AXES_NO_PARENT_ROTATION",
            "Data.p4k system SOC + local StarBreaker/Blender evidence: Quaternion(rotation) uses WXYZ identity (1,0,0,0); SCENE_AXIS_CONVERSION is a proper rotation with determinant +1 (no handedness reflection)",
            "LOCAL_DIRECT_PLUS_LOCAL_CODE_EVIDENCE");
    }

    private static bool Nearly(double a, double b) => Math.Abs(a - b) <= 1e-6;
}

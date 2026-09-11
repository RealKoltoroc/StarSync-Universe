using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Transforms;

public sealed record BodyRotationModel(
    string BodyId,
    Vector3D Axis,
    double PeriodSeconds,
    DateTimeOffset? AlignmentUtc,
    double? AlignmentAngleRadians,
    string SourceAuthority,
    string DataStatus)
{
    public bool HasAbsolutePhase => AlignmentUtc.HasValue && AlignmentAngleRadians.HasValue && PeriodSeconds > 0;
}

public static class BodyRotationEngine
{
    public static BodyRotationModel FromPhysical(CelestialBodyPhysical body, string bodyId)
    {
        return new BodyRotationModel(
            bodyId,
            new Vector3D(body.AxisX, body.AxisY, body.AxisZ),
            body.RotationPeriodSeconds,
            AlignmentUtc: null,
            AlignmentAngleRadians: null,
            body.SourceAuthority,
            body.RotationPeriodSeconds > 0 ? "LOCAL_DIRECT_PERIOD_PHASE_UNRESOLVED" : "LOCAL_DIRECT_STATIC_OR_UNRESOLVED");
    }

    public static double RelativeAngleRadians(BodyRotationModel model, TimeSpan elapsed)
    {
        if (model.PeriodSeconds <= 0) return 0d;
        var cycles = elapsed.TotalSeconds / model.PeriodSeconds;
        return NormalizeRadians(cycles * Math.Tau);
    }

    public static double AbsoluteAngleRadians(BodyRotationModel model, DateTimeOffset utc)
    {
        if (!model.HasAbsolutePhase)
            throw new InvalidOperationException($"Absolute spin phase for {model.BodyId} is not locally proven/calibrated.");

        var elapsed = utc - model.AlignmentUtc!.Value;
        var angle = model.AlignmentAngleRadians!.Value + RelativeAngleRadians(model, elapsed);
        return NormalizeRadians(angle);
    }

    public static QuaternionD RelativeRotation(BodyRotationModel model, TimeSpan elapsed) =>
        QuaternionD.FromAxisAngle(model.Axis, RelativeAngleRadians(model, elapsed));

    public static QuaternionD AbsoluteRotation(BodyRotationModel model, DateTimeOffset utc) =>
        QuaternionD.FromAxisAngle(model.Axis, AbsoluteAngleRadians(model, utc));

    public static double NormalizeRadians(double radians)
    {
        radians %= Math.Tau;
        return radians < 0 ? radians + Math.Tau : radians;
    }
}

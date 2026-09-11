namespace StarSyncUniverse.Transforms;

public static class BodyFrameTransformEngine
{
    // Relative-only transform. It is safe without an absolute phase because both directions use the same elapsed reference.
    public static Vector3D BodyFixedToSystemRelative(
        Vector3D bodyCenterSystem,
        Vector3D bodyFixedPoint,
        BodyRotationModel rotation,
        TimeSpan elapsedFromReference)
    {
        var spin = BodyRotationEngine.RelativeRotation(rotation, elapsedFromReference);
        return bodyCenterSystem + spin.Rotate(bodyFixedPoint);
    }

    public static Vector3D SystemToBodyFixedRelative(
        Vector3D bodyCenterSystem,
        Vector3D systemPoint,
        BodyRotationModel rotation,
        TimeSpan elapsedFromReference)
    {
        var spin = BodyRotationEngine.RelativeRotation(rotation, elapsedFromReference).Conjugate().Normalize();
        return spin.Rotate(systemPoint - bodyCenterSystem);
    }

    public static Vector3D BodyFixedToSystemAbsolute(
        Vector3D bodyCenterSystem,
        Vector3D bodyFixedPoint,
        BodyRotationModel rotation,
        DateTimeOffset utc)
    {
        var spin = BodyRotationEngine.AbsoluteRotation(rotation, utc);
        return bodyCenterSystem + spin.Rotate(bodyFixedPoint);
    }

    public static Vector3D SystemToBodyFixedAbsolute(
        Vector3D bodyCenterSystem,
        Vector3D systemPoint,
        BodyRotationModel rotation,
        DateTimeOffset utc)
    {
        var spin = BodyRotationEngine.AbsoluteRotation(rotation, utc).Conjugate().Normalize();
        return spin.Rotate(systemPoint - bodyCenterSystem);
    }
}

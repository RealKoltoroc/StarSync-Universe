using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Transforms;

public readonly record struct Vector3D(double X, double Y, double Z)
{
    public static Vector3D Zero => new(0, 0, 0);
    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    public Vector3D Normalize()
    {
        var length = Length;
        return length <= double.Epsilon ? Zero : this / length;
    }

    public static Vector3D operator +(Vector3D a, Vector3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vector3D operator -(Vector3D a, Vector3D b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vector3D operator *(Vector3D v, double s) => new(v.X * s, v.Y * s, v.Z * s);
    public static Vector3D operator /(Vector3D v, double s) => new(v.X / s, v.Y / s, v.Z / s);

    public static double Dot(Vector3D a, Vector3D b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    public static Vector3D Cross(Vector3D a, Vector3D b) => new(
        a.Y * b.Z - a.Z * b.Y,
        a.Z * b.X - a.X * b.Z,
        a.X * b.Y - a.Y * b.X);
}

public readonly record struct QuaternionD(double W, double X, double Y, double Z)
{
    public static QuaternionD Identity => new(1, 0, 0, 0);

    public QuaternionD Normalize()
    {
        var n = Math.Sqrt(W * W + X * X + Y * Y + Z * Z);
        return n <= double.Epsilon ? Identity : new QuaternionD(W / n, X / n, Y / n, Z / n);
    }

    public QuaternionD Conjugate() => new(W, -X, -Y, -Z);

    public static QuaternionD operator *(QuaternionD a, QuaternionD b) => new(
        a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z,
        a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
        a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
        a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W);

    public Vector3D Rotate(Vector3D v)
    {
        var q = Normalize();
        var p = new QuaternionD(0, v.X, v.Y, v.Z);
        var r = q * p * q.Conjugate();
        return new Vector3D(r.X, r.Y, r.Z);
    }

    public static QuaternionD FromAxisAngle(Vector3D axis, double angleRadians)
    {
        var n = axis.Normalize();
        if (n.Length <= double.Epsilon) return Identity;
        var half = angleRadians * 0.5d;
        var s = Math.Sin(half);
        return new QuaternionD(Math.Cos(half), n.X * s, n.Y * s, n.Z * s).Normalize();
    }
}

public readonly record struct RigidTransformD(Vector3D Translation, QuaternionD Rotation)
{
    public static RigidTransformD Identity => new(Vector3D.Zero, QuaternionD.Identity);

    public Vector3D TransformPoint(Vector3D local) => Translation + Rotation.Rotate(local);

    public Vector3D InverseTransformPoint(Vector3D world)
    {
        var inv = Rotation.Normalize().Conjugate();
        return inv.Rotate(world - Translation);
    }

    public RigidTransformD Combine(RigidTransformD child)
    {
        var rotation = Rotation.Normalize() * child.Rotation.Normalize();
        var translation = Translation + Rotation.Normalize().Rotate(child.Translation);
        return new RigidTransformD(translation, rotation.Normalize());
    }
}

public static class PlacementMath
{
    public static Vector3D WorldPosition(SpatialPlacementRecord placement) =>
        new(placement.WorldX, placement.WorldY, placement.WorldZ);

    public static Vector3D LocalPosition(SpatialPlacementRecord placement) =>
        new(placement.LocalX, placement.LocalY, placement.LocalZ);

    // Current system SOC proof: Child.pos is parent-relative translation expressed in common system axes.
    public static Vector3D ComposeSystemAxesTranslation(Vector3D parentWorld, Vector3D local) => parentWorld + local;
}

namespace StarSyncUniverse.Transforms;

public readonly record struct SurfaceCoordinate(double LatitudeDegrees, double LongitudeDegrees, double AltitudeMeters);

public static class SurfaceCoordinateEngine
{
    public static SurfaceCoordinate CartesianToLatLonAlt(Vector3D bodyFixed, double referenceRadiusMeters)
    {
        if (referenceRadiusMeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(referenceRadiusMeters));

        var r = bodyFixed.Length;
        if (r <= double.Epsilon)
            return new SurfaceCoordinate(0, 0, -referenceRadiusMeters);

        var lat = Math.Asin(Math.Clamp(bodyFixed.Z / r, -1d, 1d));
        var lon = Math.Atan2(bodyFixed.Y, bodyFixed.X);
        return new SurfaceCoordinate(
            lat * 180d / Math.PI,
            NormalizeLongitudeDegrees(lon * 180d / Math.PI),
            r - referenceRadiusMeters);
    }

    public static Vector3D LatLonAltToCartesian(SurfaceCoordinate surface, double referenceRadiusMeters)
    {
        if (referenceRadiusMeters <= 0)
            throw new ArgumentOutOfRangeException(nameof(referenceRadiusMeters));

        var lat = surface.LatitudeDegrees * Math.PI / 180d;
        var lon = surface.LongitudeDegrees * Math.PI / 180d;
        var radius = referenceRadiusMeters + surface.AltitudeMeters;
        var cosLat = Math.Cos(lat);
        return new Vector3D(
            radius * cosLat * Math.Cos(lon),
            radius * cosLat * Math.Sin(lon),
            radius * Math.Sin(lat));
    }

    public static double NormalizeLongitudeDegrees(double degrees)
    {
        degrees %= 360d;
        if (degrees > 180d) degrees -= 360d;
        if (degrees <= -180d) degrees += 360d;
        return degrees;
    }
}

using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class SurfaceMapProjectionService
{
    public static SurfaceMapPoint ProjectEquirectangular(
        double latitudeDegrees,
        double longitudeDegrees,
        string coordinateConvention,
        string dataStatus)
    {
        var latitude = Math.Clamp(latitudeDegrees, -90d, 90d);
        var longitude = NormalizeLongitude(longitudeDegrees);
        var x = (longitude + 180d) / 360d;
        var y = (90d - latitude) / 180d;
        return new SurfaceMapPoint(x, y, latitude, longitude, coordinateConvention, dataStatus);
    }

    public static IReadOnlyList<SurfaceMapTerminatorPoint> BuildTerminator(
        double subSolarLatitudeDegrees,
        double subSolarLongitudeDegrees,
        int samples = 181)
    {
        if (samples < 16) throw new ArgumentOutOfRangeException(nameof(samples));

        var subLat = DegreesToRadians(Math.Clamp(subSolarLatitudeDegrees, -90d, 90d));
        var subLon = DegreesToRadians(NormalizeLongitude(subSolarLongitudeDegrees));
        var points = new List<SurfaceMapTerminatorPoint>(samples);

        // Great circle whose normal is the sub-solar direction. This is purely geometric;
        // callers must only provide a sub-solar direction backed by a proven time/orientation model.
        var nx = Math.Cos(subLat) * Math.Cos(subLon);
        var ny = Math.Cos(subLat) * Math.Sin(subLon);
        var nz = Math.Sin(subLat);

        var reference = Math.Abs(nz) < 0.9 ? (X: 0d, Y: 0d, Z: 1d) : (X: 1d, Y: 0d, Z: 0d);
        var ux = ny * reference.Z - nz * reference.Y;
        var uy = nz * reference.X - nx * reference.Z;
        var uz = nx * reference.Y - ny * reference.X;
        var uLen = Math.Sqrt(ux * ux + uy * uy + uz * uz);
        ux /= uLen; uy /= uLen; uz /= uLen;

        var vx = ny * uz - nz * uy;
        var vy = nz * ux - nx * uz;
        var vz = nx * uy - ny * ux;

        for (var i = 0; i < samples; i++)
        {
            var angle = 2d * Math.PI * i / (samples - 1d);
            var x3 = ux * Math.Cos(angle) + vx * Math.Sin(angle);
            var y3 = uy * Math.Cos(angle) + vy * Math.Sin(angle);
            var z3 = uz * Math.Cos(angle) + vz * Math.Sin(angle);
            var latitude = RadiansToDegrees(Math.Asin(Math.Clamp(z3, -1d, 1d)));
            var longitude = RadiansToDegrees(Math.Atan2(y3, x3));
            var map = ProjectEquirectangular(latitude, longitude, "SUBSOLAR_GEOMETRIC", "DERIVED_FROM_CALLER_SUPPLIED_SUBSOLAR_DIRECTION");
            points.Add(new SurfaceMapTerminatorPoint(map.X01, map.Y01, latitude, longitude));
        }

        return points;
    }

    private static double NormalizeLongitude(double longitudeDegrees)
    {
        var value = longitudeDegrees % 360d;
        if (value > 180d) value -= 360d;
        if (value < -180d) value += 360d;
        return value;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
    private static double RadiansToDegrees(double radians) => radians * 180d / Math.PI;
}

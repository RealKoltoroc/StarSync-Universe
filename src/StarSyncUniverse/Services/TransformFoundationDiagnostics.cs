using StarSyncUniverse.Domain;
using StarSyncUniverse.Transforms;

namespace StarSyncUniverse.Services;

public static class TransformFoundationDiagnostics
{
    public static IReadOnlyList<string> Run(UniverseDataset dataset)
    {
        var messages = new List<string>();
        ValidatePlacementTranslation(dataset, messages);
        ValidateQuaternionComponentOrder(dataset, messages);
        ValidateSurfaceRoundTrip(dataset, messages);
        ValidateBodyRotation(dataset, messages);
        return messages;
    }

    private static void ValidatePlacementTranslation(UniverseDataset dataset, List<string> messages)
    {
        var byId = dataset.Placements.ToDictionary(x => x.PlacementId, StringComparer.OrdinalIgnoreCase);
        var tested = 0;
        var maxDelta = 0d;

        foreach (var placement in dataset.Placements)
        {
            if (placement.ParentPlacementId is null || !byId.TryGetValue(placement.ParentPlacementId, out var parent))
                continue;

            var predicted = PlacementMath.ComposeSystemAxesTranslation(
                new Vector3D(parent.WorldX, parent.WorldY, parent.WorldZ),
                new Vector3D(placement.LocalX, placement.LocalY, placement.LocalZ));
            var actual = new Vector3D(placement.WorldX, placement.WorldY, placement.WorldZ);
            var delta = (predicted - actual).Length;
            tested++;
            maxDelta = Math.Max(maxDelta, delta);
        }

        messages.Add($"G2 translation proof: {tested} nested placement(s), max parent+local delta {maxDelta:N9} m.");
    }

    private static void ValidateQuaternionComponentOrder(UniverseDataset dataset, List<string> messages)
    {
        var identityWFirst = dataset.Placements.Count(p =>
            Math.Abs(p.Q0 - 1d) < 1e-6 && Math.Abs(p.Q1) < 1e-6 && Math.Abs(p.Q2) < 1e-6 && Math.Abs(p.Q3) < 1e-6);
        var identityWLast = dataset.Placements.Count(p =>
            Math.Abs(p.Q0) < 1e-6 && Math.Abs(p.Q1) < 1e-6 && Math.Abs(p.Q2) < 1e-6 && Math.Abs(p.Q3 - 1d) < 1e-6);
        messages.Add(
            $"G2 quaternion convention evidence: {dataset.System} placements={dataset.Placements.Count}, exact (1,0,0,0)={identityWFirst}, exact (0,0,0,1)={identityWLast}. " +
            "Current local data strongly supports Q0=W / WXYZ component order; handedness and active/passive orientation semantics remain unresolved.");
    }

    private static void ValidateSurfaceRoundTrip(UniverseDataset dataset, List<string> messages)
    {
        var body = dataset.Bodies.FirstOrDefault(x => x.RadiusMeters > 0);
        if (body is null)
        {
            messages.Add("G2 surface math: skipped, no body radius available.");
            return;
        }

        SurfaceCoordinate[] samples =
        [
            new(0, 0, 0),
            new(45, 90, 1234.5),
            new(-33.25, -179.5, 42),
            new(89.9, 12.34, 5000)
        ];

        var maxPositionDelta = 0d;
        foreach (var sample in samples)
        {
            var xyz = SurfaceCoordinateEngine.LatLonAltToCartesian(sample, body.RadiusMeters);
            var roundTrip = SurfaceCoordinateEngine.CartesianToLatLonAlt(xyz, body.RadiusMeters);
            var xyz2 = SurfaceCoordinateEngine.LatLonAltToCartesian(roundTrip, body.RadiusMeters);
            maxPositionDelta = Math.Max(maxPositionDelta, (xyz2 - xyz).Length);
        }

        messages.Add($"G2 surface round-trip: {samples.Length} sample(s) on {body.Name}, max Cartesian delta {maxPositionDelta:N9} m.");
    }

    private static void ValidateBodyRotation(UniverseDataset dataset, List<string> messages)
    {
        var body = dataset.Bodies.FirstOrDefault(x => x.RotationPeriodSeconds > 0 && x.RadiusMeters > 0);
        if (body is null)
        {
            messages.Add("G2 rotation math: skipped, no rotating body available.");
            return;
        }

        var model = BodyRotationEngine.FromPhysical(body, body.SourceUuid ?? body.ContainerName);
        var p = new Vector3D(body.RadiusMeters, 0, 0);
        var afterPeriod = BodyFrameTransformEngine.BodyFixedToSystemRelative(
            Vector3D.Zero, p, model, TimeSpan.FromSeconds(body.RotationPeriodSeconds));
        var fullCycleDelta = (afterPeriod - p).Length;

        var half = BodyFrameTransformEngine.BodyFixedToSystemRelative(
            Vector3D.Zero, p, model, TimeSpan.FromSeconds(body.RotationPeriodSeconds / 2d));
        var expectedHalf = new Vector3D(-body.RadiusMeters, 0, 0);
        var halfCycleDelta = (half - expectedHalf).Length;

        var recovered = BodyFrameTransformEngine.SystemToBodyFixedRelative(
            Vector3D.Zero, half, model, TimeSpan.FromSeconds(body.RotationPeriodSeconds / 2d));
        var inverseDelta = (recovered - p).Length;

        messages.Add(
            $"G2 rotation math: {body.Name}, full-cycle delta {fullCycleDelta:N9} m, half-cycle delta {halfCycleDelta:N9} m, inverse delta {inverseDelta:N9} m; absolute phase remains unresolved unless calibrated.");
    }
}

using StarSyncUniverse.Domain;
using StarSyncUniverse.Transforms;

namespace StarSyncUniverse.Services;

public static class KnownProofDiagnostics
{
    public static IReadOnlyList<string> Run(UniverseDataset dataset)
    {
        var messages = new List<string>();
        if (dataset.System.Equals("stanton", StringComparison.OrdinalIgnoreCase))
        {
            AddSurfaceAnchorProof(dataset, messages, "New Babbage");
            AddSurfaceAnchorProof(dataset, messages, "Lorville");
            AddSurfaceAnchorProof(dataset, messages, "Area18");
            AddSurfaceAnchorProof(dataset, messages, "Orison");
            AddPortTresslerProof(dataset, messages);
            AddAaronHaloProof(dataset, messages);
        }
        else if (dataset.System.Equals("pyro", StringComparison.OrdinalIgnoreCase))
        {
            messages.Add($"G1/G3 Pyro cluster proof: {dataset.Volumes.Count(v => v.Type == "AsteroidClusterVolume")} locally bounded asteroid-cluster volume(s).");
        }
        return messages;
    }

    private static void AddSurfaceAnchorProof(UniverseDataset dataset, List<string> messages, string name)
    {
        var anchor = dataset.BodyAnchors.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (anchor is null)
        {
            messages.Add($"G3 {name} proof: MISSING from current imported body anchors.");
            return;
        }

        var local = new Vector3D(anchor.BodyLocalX, anchor.BodyLocalY, anchor.BodyLocalZ);
        messages.Add(
            $"G3 {name} proof: body={anchor.BodyName}; scope={anchor.AnchorScope}; body-local XYZ=({local.X:N3},{local.Y:N3},{local.Z:N3}) m; radial={local.Length:N3} m; " +
            $"reference radius={anchor.ReferenceRadiusMeters:N3} m; geometric altitude={anchor.AltitudeMeters:N3} m; " +
            $"axis-spherical lat/lon=({anchor.AxisLatitudeDegrees:N6},{anchor.AxisLongitudeDegrees:N6}) deg; geographic convention UNPROVEN; source={anchor.SourcePath}.");
    }

    private static void AddPortTresslerProof(UniverseDataset dataset, List<string> messages)
    {
        var microTech = dataset.Entities.FirstOrDefault(e => e.Name.Equals("microTech", StringComparison.OrdinalIgnoreCase));
        var station = dataset.Entities.FirstOrDefault(e => e.Name.Contains("Port Tressler", StringComparison.OrdinalIgnoreCase));
        var body = dataset.Bodies.FirstOrDefault(b => b.Name.Equals("microTech", StringComparison.OrdinalIgnoreCase));
        if (microTech is null || station is null || body is null)
        {
            messages.Add("G1 Port Tressler proof: not present in the current imported LIVE system/body OC paths; historical/reference placement is intentionally not substituted.");
            return;
        }

        var bodyCenter = new Vector3D(microTech.X, microTech.Y, microTech.Z);
        var stationCenter = new Vector3D(station.X, station.Y, station.Z);
        var distance = (stationCenter - bodyCenter).Length;
        messages.Add(
            $"G1 Port Tressler proof: microTech center distance={distance:N3} m; reference-radius altitude={distance - body.RadiusMeters:N3} m; " +
            "derived from current Data.p4k placement hierarchy.");
    }

    private static void AddAaronHaloProof(UniverseDataset dataset, List<string> messages)
    {
        var halo = dataset.Regions.FirstOrDefault(r => r.Name.Equals("Aaron Halo", StringComparison.OrdinalIgnoreCase));
        if (halo is null)
        {
            messages.Add("G1 Aaron Halo proof: MISSING.");
            return;
        }

        messages.Add(
            $"G1 Aaron Halo proof: inner={halo.InnerRadiusMeters:N0} m; outer={halo.OuterRadiusMeters:N0} m; thickness={halo.ThicknessMeters:N0} m; density={halo.DensityScale:G}; {halo.DataStatus}.");
    }
}

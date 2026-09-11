namespace StarSyncUniverse.Services;

/// <summary>
/// Presentation-only color references measured from CURRENT Alpha 4.0/PTU orbital screenshots hosted by
/// starcitizen.tools. The source images are not shipped with StarSyncUniverse; only derived RGB statistics and
/// source URLs are retained so CURRENT LIVE terrain-control geometry can be colored toward the in-game appearance.
/// </summary>
public readonly record struct PresentationRgb(double R, double G, double B);

public sealed record PyroReferenceColorProfile(
    string Container,
    string BodyName,
    PresentationRgb Dark,
    PresentationRgb Mid,
    PresentationRgb Light,
    PresentationRgb Warm,
    PresentationRgb Cool,
    PresentationRgb Mean,
    string ReferenceUrl,
    bool GasGiant = false,
    bool ForceCloudFree = false,
    int OutputSize = 4096);

public static class PyroReferenceColorProfiles
{
    private static readonly IReadOnlyDictionary<string, PyroReferenceColorProfile> Profiles =
        new Dictionary<string, PyroReferenceColorProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["pyro1"] = P(
                "pyro1", "Pyro I",
                D(0.003, 0.666, 0.972), D(0.005, 0.700, 0.976), D(0.146, 0.778, 0.982),
                D(0.005, 0.700, 0.976), D(0.000, 0.683, 0.971), D(0.092, 0.719, 0.977),
                "https://starcitizen.tools/File:Pyro-PyroI-orbit-4.0PTU.jpg"),
            ["pyro2"] = P(
                "pyro2", "Monox",
                D(0.460, 0.488, 0.590), D(0.571, 0.544, 0.662), D(0.671, 0.590, 0.713),
                D(0.787, 0.594, 0.565), D(0.459, 0.521, 0.643), D(0.569, 0.537, 0.641),
                "https://starcitizen.tools/File:Pyro-Monox-orbit-4.0PTU_11.jpg",
                ForceCloudFree: true),
            ["pyro3"] = P(
                "pyro3", "Bloom",
                D(0.179, 0.437, 0.657), D(0.242, 0.482, 0.695), D(0.624, 0.674, 0.749),
                D(0.242, 0.482, 0.695), D(0.159, 0.439, 0.673), D(0.389, 0.550, 0.701),
                "https://starcitizen.tools/File:Pyro-bloom-orbit-4.0PTU_01.jpg"),
            ["pyro4"] = P(
                "pyro4", "Pyro IV",
                D(0.396, 0.536, 0.483), D(0.679, 0.614, 0.513), D(0.766, 0.735, 0.634),
                D(0.737, 0.549, 0.425), D(0.273, 0.530, 0.503), D(0.609, 0.626, 0.549),
                "https://starcitizen.tools/File:Pyro-PyroIV-orbit-4.0PTU.jpg"),
            ["pyro5"] = P(
                "pyro5", "Pyro V",
                D(0.839, 0.858, 0.496), D(0.894, 0.887, 0.542), D(0.927, 0.905, 0.573),
                D(0.869, 0.864, 0.477), D(0.894, 0.887, 0.542), D(0.880, 0.879, 0.535),
                "https://starcitizen.tools/File:Pyro-PyroV-orbit-4.0PTU.jpg",
                GasGiant: true),
            ["pyro5a"] = P(
                "pyro5a", "Ignis",
                D(0.814, 0.629, 0.568), D(0.863, 0.659, 0.583), D(0.911, 0.686, 0.597),
                D(0.929, 0.670, 0.563), D(0.863, 0.659, 0.583), D(0.862, 0.659, 0.582),
                "https://starcitizen.tools/File:Pyro-PyroV-Ignis-orbit-4.0PTU.jpg"),
            ["pyro5b"] = P(
                "pyro5b", "Vatra",
                D(0.297, 0.308, 0.253), D(0.476, 0.492, 0.412), D(0.602, 0.613, 0.518),
                D(0.342, 0.318, 0.261), D(0.476, 0.492, 0.412), D(0.452, 0.462, 0.388),
                "https://starcitizen.tools/File:Pyro-PyroV-Vatra-orbit-4.0PTU.jpg"),
            ["pyro5c"] = P(
                "pyro5c", "Adir",
                D(0.559, 0.477, 0.448), D(0.671, 0.600, 0.560), D(0.762, 0.681, 0.628),
                D(0.700, 0.573, 0.511), D(0.167, 0.208, 0.238), D(0.659, 0.583, 0.544),
                "https://starcitizen.tools/File:Pyro-PyroV-Adir-orbit-4.0PTU.jpg"),
            ["pyro5d"] = P(
                "pyro5d", "Fairo",
                D(0.537, 0.237, 0.250), D(0.659, 0.277, 0.270), D(0.800, 0.328, 0.260),
                D(0.832, 0.317, 0.222), D(0.659, 0.277, 0.270), D(0.661, 0.289, 0.257),
                "https://starcitizen.tools/File:Pyro-PyroV-Fairo-orbit-4.0PTU.jpg"),
            ["pyro5e"] = P(
                "pyro5e", "Fuego",
                D(0.677, 0.634, 0.540), D(0.760, 0.696, 0.582), D(0.817, 0.730, 0.582),
                D(0.867, 0.708, 0.461), D(0.760, 0.696, 0.582), D(0.747, 0.676, 0.548),
                "https://starcitizen.tools/File:Pyro-PyroV-Fuego-orbit-4.0PTU.jpg",
                ForceCloudFree: true),
            ["pyro5f"] = P(
                "pyro5f", "Vuur",
                D(0.644, 0.690, 0.639), D(0.673, 0.710, 0.658), D(0.708, 0.736, 0.680),
                D(0.773, 0.741, 0.665), D(0.600, 0.663, 0.651), D(0.679, 0.714, 0.659),
                "https://starcitizen.tools/File:Pyro-PyroV-Vuur-orbit-4.0PTU.jpg"),
            ["pyro6"] = P(
                "pyro6", "Terminus",
                D(0.363, 0.317, 0.296), D(0.479, 0.413, 0.374), D(0.638, 0.538, 0.458),
                D(0.569, 0.467, 0.384), D(0.479, 0.413, 0.374), D(0.501, 0.434, 0.386),
                "https://starcitizen.tools/File:Pyro-Terminus-orbit-4.0PTU.jpg",
                ForceCloudFree: true)
        };

    public static PyroReferenceColorProfile? Find(string? container) =>
        !string.IsNullOrWhiteSpace(container) && Profiles.TryGetValue(container.Trim(), out var profile)
            ? profile
            : null;

    public static IReadOnlyCollection<PyroReferenceColorProfile> All => Profiles.Values.ToArray();

    private static PresentationRgb D(double r, double g, double b) => new(r, g, b);

    private static PyroReferenceColorProfile P(
        string container,
        string bodyName,
        PresentationRgb dark,
        PresentationRgb mid,
        PresentationRgb light,
        PresentationRgb warm,
        PresentationRgb cool,
        PresentationRgb mean,
        string referenceUrl,
        bool GasGiant = false,
        bool ForceCloudFree = false,
        int OutputSize = 4096) =>
        new(container, bodyName, dark, mid, light, warm, cool, mean, referenceUrl, GasGiant, ForceCloudFree, OutputSize);
}

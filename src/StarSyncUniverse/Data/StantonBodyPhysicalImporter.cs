using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Data;

public sealed class StantonBodyPhysicalImporter
{
    private readonly StarBreakerClient _starBreaker;

    public StantonBodyPhysicalImporter(StarBreakerClient starBreaker) => _starBreaker = starBreaker;

    public async Task<IReadOnlyList<CelestialBodyPhysical>> ImportAsync(
        IReadOnlyList<UniverseEntity> spatialEntities,
        CancellationToken cancellationToken = default)
    {
        const string filter = "Data/ObjectContainers/PU/system/stanton/stanton*.socpak";
        var extractRoot = await _starBreaker.ExtractAsync(filter, "stanton-bodies", cancellationToken);
        var results = new List<CelestialBodyPhysical>();

        foreach (var socpak in Directory.EnumerateFiles(extractRoot, "stanton*.socpak", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var container = Path.GetFileNameWithoutExtension(socpak);
            if (container.Equals("stantonsystem", StringComparison.OrdinalIgnoreCase) ||
                container.Equals("stantonstar", StringComparison.OrdinalIgnoreCase) ||
                container.Contains("skybox", StringComparison.OrdinalIgnoreCase) ||
                container.Contains("planet_only", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                using var zip = ZipFile.OpenRead(socpak);
                var xmlEntry = zip.Entries.FirstOrDefault(e => e.FullName.Equals(container + ".xml", StringComparison.OrdinalIgnoreCase));
                if (xmlEntry is null) continue;
                using var reader = new StreamReader(xmlEntry.Open());
                var doc = XDocument.Load(reader);
                var planet = doc.Descendants("Entity").FirstOrDefault(e => e.Attribute("planetRadius") is not null);
                if (planet is null) continue;

                var radius = Parse(planet.Attribute("planetRadius")?.Value);
                var rotationHours = Parse(planet.Attribute("planetRotationSpeed")?.Value);
                var axis = ParseVector3(planet.Attribute("planetAxis")?.Value);

                var entity = spatialEntities.FirstOrDefault(e =>
                    (e.Type.Equals("Planet", StringComparison.OrdinalIgnoreCase) || e.Type.Equals("Moon", StringComparison.OrdinalIgnoreCase)) &&
                    (e.SourcePath?.Replace('\\','/').EndsWith('/' + container + ".socpak", StringComparison.OrdinalIgnoreCase) ?? false));

                results.Add(new CelestialBodyPhysical(
                    SourcePath: filter.Replace("stanton*.socpak", container + ".socpak"),
                    ContainerName: container,
                    SourceUuid: entity?.SourceUuid,
                    Name: entity?.Name ?? container,
                    RadiusMeters: radius,
                    RotationPeriodHoursRaw: rotationHours,
                    RotationPeriodSeconds: rotationHours * 3600d,
                    AxisX: axis.X,
                    AxisY: axis.Y,
                    AxisZ: axis.Z,
                    SourceAuthority: "Data.p4k/Body ObjectContainer",
                    DataStatus: "LOCAL_DIRECT"));
            }
            catch (InvalidDataException)
            {
                // Non-zip or unsupported containers are ignored and remain discoverable through diagnostics.
            }
        }

        return results.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static double Parse(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0d;

    private static (double X, double Y, double Z) ParseVector3(string? value)
    {
        var p = (value ?? string.Empty).Split(',');
        return (
            p.Length > 0 ? Parse(p[0]) : 0,
            p.Length > 1 ? Parse(p[1]) : 0,
            p.Length > 2 ? Parse(p[2]) : 0);
    }
}

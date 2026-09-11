using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Data;

public sealed class BodyPhysicalImporter
{
    private readonly StarBreakerClient _starBreaker;

    public BodyPhysicalImporter(StarBreakerClient starBreaker) => _starBreaker = starBreaker;

    public async Task<IReadOnlyList<CelestialBodyPhysical>> ImportAsync(
        string system,
        IReadOnlyList<UniverseEntity> spatialEntities,
        CancellationToken cancellationToken = default)
    {
        system = system.Trim().ToLowerInvariant();
        var filter = $"Data/ObjectContainers/PU/system/{system}/{system}*.socpak";
        var extractRoot = await _starBreaker.ExtractAsync(filter, $"{system}-bodies", cancellationToken);
        var results = new List<CelestialBodyPhysical>();

        foreach (var socpak in Directory.EnumerateFiles(extractRoot, $"{system}*.socpak", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var container = Path.GetFileNameWithoutExtension(socpak);
            if (container.Equals(system + "system", StringComparison.OrdinalIgnoreCase) ||
                container.Equals(system + "star", StringComparison.OrdinalIgnoreCase) ||
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
                    e.SourcePath?.Replace('\\','/').EndsWith('/' + container + ".socpak", StringComparison.OrdinalIgnoreCase) ?? false);

                // Some current system SOCs (notably Nyx) place live celestial records through dummy/test OC paths.
                // In that case the physical body container still follows nyx1/nyx2/nyx3 while the starmap identity is Nyx I/II/III.
                entity ??= spatialEntities.FirstOrDefault(e =>
                    (e.Type.Equals("Planet", StringComparison.OrdinalIgnoreCase) || e.Type.Equals("Moon", StringComparison.OrdinalIgnoreCase)) &&
                    MatchesOrdinalBodyName(system, container, e.Name));

                results.Add(new CelestialBodyPhysical(
                    SourcePath: $"Data/ObjectContainers/PU/system/{system}/{container}.socpak",
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
                // Unsupported/non-zip containers remain discoverable through system diagnostics.
            }
        }

        return results.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool MatchesOrdinalBodyName(string system, string container, string name)
    {
        if (!container.StartsWith(system, StringComparison.OrdinalIgnoreCase)) return false;
        var suffix = container[system.Length..];
        if (!int.TryParse(suffix, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ordinal)) return false;
        var roman = ordinal switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", _ => null };
        return roman is not null && name.Equals($"{system} {roman}", StringComparison.OrdinalIgnoreCase);
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

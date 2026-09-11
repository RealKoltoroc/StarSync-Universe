using System.IO.Compression;
using System.Xml.Linq;
using StarSyncUniverse.Data;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public sealed class RotationPhaseEvidenceBuilder
{
    private static readonly string[] CandidateTokens =
    [
        "phase", "epoch", "meridian", "longitude", "latitude", "rotationoffset", "rotation_offset",
        "startangle", "start_angle", "initialangle", "initial_angle", "alignment", "spinphase", "spin_phase"
    ];

    private readonly StarBreakerClient _starBreaker;

    public RotationPhaseEvidenceBuilder(StarBreakerClient starBreaker) => _starBreaker = starBreaker;

    public async Task<RotationPhaseEvidenceRecord> BuildAsync(UniverseDataset dataset, CancellationToken cancellationToken = default)
    {
        var system = dataset.System.Trim().ToLowerInvariant();
        var root = await _starBreaker.ExtractAsync(
            $"Data/ObjectContainers/PU/system/{system}/{system}*.socpak",
            $"{system}-bodies",
            cancellationToken);

        var scanned = 0;
        var rotationSpeed = false;
        var axis = false;
        var phaseFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var body in dataset.Bodies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Directory.EnumerateFiles(root, body.ContainerName + ".socpak", SearchOption.AllDirectories).FirstOrDefault();
            if (path is null) continue;

            try
            {
                using var zip = ZipFile.OpenRead(path);
                var entry = zip.Entries.FirstOrDefault(e => e.FullName.Equals(body.ContainerName + ".xml", StringComparison.OrdinalIgnoreCase));
                if (entry is null) continue;
                using var reader = new StreamReader(entry.Open());
                var doc = XDocument.Load(reader);
                scanned++;

                var planetEntity = doc.Descendants("Entity").FirstOrDefault(e => e.Attribute("planetRadius") is not null);
                if (planetEntity is null) continue;
                foreach (var attribute in planetEntity.Attributes())
                {
                    var name = attribute.Name.LocalName;
                    if (name.Equals("planetRotationSpeed", StringComparison.OrdinalIgnoreCase)) rotationSpeed = true;
                    if (name.Equals("planetAxis", StringComparison.OrdinalIgnoreCase)) axis = true;
                    var normalized = name.Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
                    if (CandidateTokens.Any(token => normalized.Contains(token.Replace("_", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!name.Equals("planetRotationSpeed", StringComparison.OrdinalIgnoreCase) &&
                            !name.Equals("planetAxis", StringComparison.OrdinalIgnoreCase))
                            phaseFields.Add(name);
                    }
                }
            }
            catch (InvalidDataException)
            {
                // Unsupported container: absence is reflected in ContainersScanned.
            }
        }

        return new RotationPhaseEvidenceRecord(
            system,
            scanned,
            rotationSpeed,
            axis,
            phaseFields.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            "Current LIVE Data.p4k body root ObjectContainer XML",
            phaseFields.Count == 0
                ? "NO_ABSOLUTE_PHASE_FIELD_FOUND_IN_BODY_ROOT_XML"
                : "ABSOLUTE_PHASE_CANDIDATE_FIELDS_REQUIRE_SEMANTIC_VALIDATION");
    }
}

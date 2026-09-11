using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using StarSyncUniverse.Data;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

/// <summary>
/// Detects external station freight elevators from the CURRENT LIVE ObjectContainer graph.
///
/// Authority rules:
/// - A positive result requires a direct ObjectContainer child reference to
///   Data/ObjectContainers/PU/loc/mod/common/ext_cargo/station_ext_cargo_elevator_001.socpak.
/// - SCUnpacked's "Loading Dock" amenity is secondary corroboration only. It must never turn a
///   negative LIVE result into a positive one because hangar/internal cargo facilities are separate.
/// - Hangar_FreightElevator_* and generic CargoGrid records are intentionally excluded.
/// </summary>
public static class ExternalFreightElevatorDetector
{
    public const string InfrastructureType = "ExternalFreightElevator";
    public const string ExternalCargoContainerPath =
        "Data/ObjectContainers/PU/loc/mod/common/ext_cargo/station_ext_cargo_elevator_001.socpak";
    public const string ExternalCargoGridClass = "Exterior_FreightElevator_CargoGrid";
    public const string ExternalCargoGridUuid = "52b05758-e01a-492a-adbb-7979a62b0b86";

    public static async Task PopulateAsync(
        IReadOnlyDictionary<string, UniverseDataset> datasets,
        StarBreakerClient starBreaker,
        ScUnpackedKnowledgeDatabase? knowledgeDatabase,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!starBreaker.IsAvailable) return;

        foreach (var dataset in datasets.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            dataset.Infrastructure.RemoveAll(x =>
                x.InfrastructureType.Equals(InfrastructureType, StringComparison.OrdinalIgnoreCase));

            var candidates = dataset.Entities
                .Where(IsStationInfrastructureCandidate)
                .Where(x => !string.IsNullOrWhiteSpace(x.SourcePath) &&
                            x.SourcePath.EndsWith(".socpak", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var byPath = candidates
                .GroupBy(x => NormalizePath(x.SourcePath!), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var scanned = 0;
            foreach (var group in byPath)
            {
                cancellationToken.ThrowIfCancellationRequested();
                scanned++;
                progress?.Report($"Scanning station cargo infrastructure {dataset.System.ToUpperInvariant()} {scanned}/{byPath.Length}...");

                var evidence = await InspectContainerAsync(starBreaker, group.Key, cancellationToken);
                foreach (var entity in group)
                {
                    var scMatch = knowledgeDatabase?.MatchLocation(
                        entity.SourceUuid,
                        entity.Name,
                        dataset.System,
                        entity.ParentSourceUuid,
                        entity.SourcePath);
                    var scLoadingDock = scMatch?.Location.Services.FirstOrDefault(x =>
                        x.Name.Equals("Loading Dock", StringComparison.OrdinalIgnoreCase));
                    var corroborated = scLoadingDock is not null;
                    var scEvidence = corroborated
                        ? $"SCUnpacked Amenity: {scLoadingDock!.Name} ({scLoadingDock.Uuid})"
                        : null;

                    var status = evidence.Count > 0
                        ? corroborated
                            ? "LOCAL_DIRECT_EXTERNAL_FREIGHT_ELEVATOR_SCUNPACKED_LOADING_DOCK_CORROBORATED"
                            : "LOCAL_DIRECT_EXTERNAL_FREIGHT_ELEVATOR"
                        : corroborated
                            ? "LOCAL_DIRECT_NO_EXTERNAL_FREIGHT_ELEVATOR_SCUNPACKED_LOADING_DOCK_ONLY"
                            : "LOCAL_DIRECT_NO_EXTERNAL_FREIGHT_ELEVATOR";

                    dataset.Infrastructure.Add(new LocationInfrastructureRecord(
                        entity.Id,
                        dataset.System,
                        InfrastructureType,
                        evidence.Count > 0,
                        evidence.Count,
                        corroborated,
                        scEvidence,
                        entity.SourcePath!,
                        evidence.EvidencePath,
                        "Data.p4k CURRENT LIVE ObjectContainer child reference; SCUnpacked semantic corroboration only",
                        status));
                }
            }

            var positives = dataset.Infrastructure.Count(x =>
                x.InfrastructureType.Equals(InfrastructureType, StringComparison.OrdinalIgnoreCase) && x.IsPresent);
            dataset.Diagnostics.Add(
                $"External freight elevator scan: {positives} confirmed placement(s) / {candidates.Length} station-like candidate(s); " +
                $"class={ExternalCargoGridClass}; uuid={ExternalCargoGridUuid}; container={ExternalCargoContainerPath}.");
        }
    }

    private static bool IsStationInfrastructureCandidate(UniverseEntity entity)
    {
        var path = NormalizePath(entity.SourcePath ?? string.Empty);
        var entityClass = entity.EntityClass ?? string.Empty;
        var type = entity.Type ?? string.Empty;
        var name = entity.Name ?? string.Empty;

        return path.Contains("/station/", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("reststop", StringComparison.OrdinalIgnoreCase) ||
               path.Contains("gateway", StringComparison.OrdinalIgnoreCase) ||
               entityClass.Contains("Station", StringComparison.OrdinalIgnoreCase) ||
               type.Contains("Station", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Station", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Gateway", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Harbor", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Baijini", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Tressler", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("Grim HEX", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ContainerEvidence> InspectContainerAsync(
        StarBreakerClient starBreaker,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        try
        {
            // StarBreaker/p4k filtering is case-sensitive for archive paths. Imported SOC references
            // often preserve CIG's lower-case `objectcontainers` spelling even though the archive
            // entry uses `ObjectContainers`, so canonicalize that archive prefix before extraction.
            var canonicalSourcePath = CanonicalizeP4kPath(sourcePath);
            var cacheName = "station-infra-" + Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonicalSourcePath))).Substring(0, 20).ToLowerInvariant();
            var fileName = Path.GetFileName(canonicalSourcePath.Replace('/', Path.DirectorySeparatorChar));
            // Match by filename instead of the imported full path because Data.p4k archive filtering is
            // case-sensitive while CIG SOC references are not consistently cased. If a filename exists
            // more than once, prefer the extracted relative path matching the canonical source path.
            var extractRoot = await starBreaker.ExtractAsync($"**/{fileName}", cacheName, cancellationToken);
            var candidates = Directory.EnumerateFiles(extractRoot, fileName, SearchOption.AllDirectories).ToArray();
            var socpak = candidates.FirstOrDefault(x =>
            {
                var relative = NormalizePath(Path.GetRelativePath(extractRoot, x));
                return relative.Equals(canonicalSourcePath, StringComparison.OrdinalIgnoreCase);
            }) ?? candidates.FirstOrDefault();
            if (socpak is null) return new ContainerEvidence(0, canonicalSourcePath + "#socpak-not-found");

            using var zip = ZipFile.OpenRead(socpak);
            var rootXmlName = Path.GetFileNameWithoutExtension(fileName) + ".xml";
            var entry = zip.Entries.FirstOrDefault(x =>
                Path.GetFileName(x.FullName).Equals(rootXmlName, StringComparison.OrdinalIgnoreCase));
            if (entry is null) return new ContainerEvidence(0, sourcePath + "#root-xml-missing");

            using var stream = entry.Open();
            var document = XDocument.Load(stream, LoadOptions.None);
            var count = document
                .Descendants("Child")
                .Count(x => NormalizePath(x.Attribute("name")?.Value ?? string.Empty)
                    .Equals(ExternalCargoContainerPath, StringComparison.OrdinalIgnoreCase));

            return new ContainerEvidence(count, sourcePath + "#" + rootXmlName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // A failed/unsupported container is deliberately represented as not proven. The detector
            // never invents a positive result from a filename, trade service, or station category.
            return new ContainerEvidence(0, sourcePath + "#scan-unresolved");
        }
    }

    private static string NormalizePath(string value) => value.Replace('\\', '/').Trim();

    private static string CanonicalizeP4kPath(string value)
    {
        var normalized = NormalizePath(value);
        const string importedPrefix = "Data/objectcontainers/";
        if (normalized.StartsWith(importedPrefix, StringComparison.OrdinalIgnoreCase))
            return "Data/ObjectContainers/" + normalized[importedPrefix.Length..];
        return normalized;
    }

    private sealed record ContainerEvidence(int Count, string EvidencePath);
}

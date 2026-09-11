using System.Text.Json;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public sealed record RacingTrackReference(
    string Name,
    string System,
    string BodyName,
    string ResolvedLocationUuid,
    string? SourceUuid,
    string MatchToken,
    string Operator,
    double LengthKm,
    IReadOnlyList<string> Features,
    string Description,
    string SourceContract,
    string SemanticAuthority);

/// <summary>
/// Packaged semantic identity for racing ObjectContainers whose Data.p4k anchors do not always carry
/// a starmap UUID. The match is deliberately narrow: exact SourceUuid when available, otherwise an
/// exact racing source-path token plus the expected system/body. Data.p4k remains spatial authority.
/// </summary>
public static class RacingTrackCatalog
{
    private sealed class CatalogDocument
    {
        public int SchemaVersion { get; set; }
        public List<RacingTrackReference> Tracks { get; set; } = [];
    }

    private static readonly Lazy<IReadOnlyList<RacingTrackReference>> Entries = new(Load);

    public static IReadOnlyList<RacingTrackReference> All => Entries.Value;

    public static RacingTrackReference? Resolve(BodyLocalAnchorRecord anchor) =>
        Resolve(anchor.System, anchor.BodyName, anchor.SourcePath, anchor.SourceUuid, anchor.Name);

    public static RacingTrackReference? Resolve(SurfaceTargetCatalogEntry target) =>
        Resolve(target.System, target.BodyName, target.SourcePath, target.SourceUuid, target.Name);

    public static RacingTrackReference? Resolve(
        string? system,
        string? bodyName,
        string? sourcePath,
        string? sourceUuid,
        string? rawName)
    {
        var normalizedSystem = Normalize(system);
        var normalizedBody = Normalize(bodyName);
        var normalizedPath = NormalizePath(sourcePath);
        var normalizedName = Normalize(rawName).Replace(' ', '_');

        foreach (var entry in Entries.Value)
        {
            if (!string.Equals(Normalize(entry.System), normalizedSystem, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Normalize(entry.BodyName), normalizedBody, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrWhiteSpace(sourceUuid) &&
                !string.IsNullOrWhiteSpace(entry.SourceUuid) &&
                string.Equals(sourceUuid.Trim(), entry.SourceUuid.Trim(), StringComparison.OrdinalIgnoreCase))
                return entry;

            var token = Normalize(entry.MatchToken).Replace(' ', '_');
            if (token.Length > 0 &&
                (normalizedPath.Contains(token, StringComparison.OrdinalIgnoreCase) ||
                 normalizedName.Contains(token, StringComparison.OrdinalIgnoreCase)))
                return entry;
        }

        return null;
    }

    public static bool LooksLikeRacingAnchor(BodyLocalAnchorRecord anchor)
    {
        var text = (Normalize(anchor.Name) + " " + NormalizePath(anchor.SourcePath)).ToLowerInvariant();
        return text.Contains("rctrk", StringComparison.Ordinal) ||
               text.Contains("racetrack", StringComparison.Ordinal) ||
               text.Contains("racing_static", StringComparison.Ordinal) ||
               text.Contains("racing static", StringComparison.Ordinal);
    }

    private static IReadOnlyList<RacingTrackReference> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "CommunityBaseline", "Racing", "racing-tracks.json");
        if (!File.Exists(path))
        {
            var repositoryRoot = RepositoryLocator.FindRepositoryRoot();
            if (!string.IsNullOrWhiteSpace(repositoryRoot))
                path = Path.Combine(repositoryRoot, "src", "StarSyncUniverse", "Assets", "CommunityBaseline", "Racing", "racing-tracks.json");
        }

        if (!File.Exists(path)) return [];
        try
        {
            var document = JsonSerializer.Deserialize<CatalogDocument>(File.ReadAllText(path), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            });
            return document?.Tracks
                .Where(x => !string.IsNullOrWhiteSpace(x.Name) && !string.IsNullOrWhiteSpace(x.MatchToken))
                .ToArray() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizePath(string? value) =>
        Normalize(value).Replace('\\', '/');
}

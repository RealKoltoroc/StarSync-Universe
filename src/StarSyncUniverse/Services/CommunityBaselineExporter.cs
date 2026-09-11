using System.Text.Json;

namespace StarSyncUniverse.Services;

public static class CommunityBaselineExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static async Task<string> ExportAsync(
        string repositoryRoot,
        ScUnpackedKnowledgeDatabase knowledge,
        CancellationToken cancellationToken = default)
    {
        var projectRoot = Path.Combine(repositoryRoot, "src", "StarSyncUniverse");
        var assetRoot = Path.Combine(projectRoot, "Assets", "CommunityBaseline");
        var universeRoot = Path.Combine(assetRoot, "Universe");
        var knowledgeRoot = Path.Combine(assetRoot, "Knowledge");
        Directory.CreateDirectory(universeRoot);
        Directory.CreateDirectory(knowledgeRoot);

        var localSnapshotRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StarSyncUniverse", "Snapshots");
        if (!Directory.Exists(localSnapshotRoot))
            throw new DirectoryNotFoundException($"Local universe snapshots are missing: '{localSnapshotRoot}'.");

        var copiedSystems = new List<string>();
        var builds = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var system in new[] { "stanton", "pyro", "nyx" })
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = Path.Combine(localSnapshotRoot, system + ".current.json");
            if (!File.Exists(source)) continue;
            var destination = Path.Combine(universeRoot, system + ".current.json");
            File.Copy(source, destination, overwrite: true);
            copiedSystems.Add(system);

            try
            {
                using var json = JsonDocument.Parse(await File.ReadAllTextAsync(source, cancellationToken));
                if (json.RootElement.TryGetProperty("Build", out var build) && build.ValueKind == JsonValueKind.String)
                    builds[system] = build.GetString() ?? string.Empty;
                else if (json.RootElement.TryGetProperty("build", out build) && build.ValueKind == JsonValueKind.String)
                    builds[system] = build.GetString() ?? string.Empty;
            }
            catch (JsonException)
            {
                builds[system] = "unknown";
            }
        }

        if (copiedSystems.Count == 0)
            throw new InvalidDataException("No per-system universe snapshots were available to freeze into the community baseline.");

        var knowledgePath = Path.Combine(knowledgeRoot, "location-knowledge.json");
        await knowledge.ExportBundledBaselineAsync(knowledgePath, cancellationToken);

        var manifest = new
        {
            schemaVersion = 1,
            generatedUtc = DateTimeOffset.UtcNow,
            purpose = "StarSyncUniverse standalone community baseline",
            systems = copiedSystems,
            builds,
            knowledge = new
            {
                locations = knowledge.Summary.LocationCount,
                factions = knowledge.Summary.FactionCount,
                services = knowledge.Summary.ServiceCount,
                tradeLocations = knowledge.Summary.TradeLocationCount,
                commodityRelationLocations = knowledge.Summary.CommodityRelationLocationCount,
                commodities = knowledge.Summary.CommodityCount,
                itemIndexCountAtFreeze = knowledge.Summary.ItemCount
            },
            runtimePolicy = "Bundled spatial snapshots + bundled canonical location knowledge are the no-tool default. Optional adapters may refresh/enrich but are never required for first start."
        };
        var manifestPath = Path.Combine(assetRoot, "manifest.json");
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);
        return manifestPath;
    }
}

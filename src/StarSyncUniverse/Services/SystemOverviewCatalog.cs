using System.Text.Json;

namespace StarSyncUniverse.Services;

public sealed record SystemOverviewStat(string Key, string Label, int Value);

public sealed record SystemOverviewRecord(
    string System,
    string DisplayName,
    string Description,
    string Affiliation,
    string Jurisdiction,
    string Size,
    string StarType,
    IReadOnlyList<SystemOverviewStat> Stats,
    string SourceName,
    string SourceUrl,
    DateTimeOffset SourceCheckedUtc);

public static class SystemOverviewCatalog
{
    private sealed record Payload(int SchemaVersion, SystemOverviewRecord[] Systems);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static SystemOverviewRecord? LoadForSystem(string system)
    {
        if (string.IsNullOrWhiteSpace(system)) return null;
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "Assets", "CommunityBaseline", "System", "system-overviews.json");
        if (!File.Exists(path)) return null;

        try
        {
            using var stream = File.OpenRead(path);
            var payload = JsonSerializer.Deserialize<Payload>(stream, Options);
            if (payload is null || payload.SchemaVersion != 1) return null;
            return (payload.Systems ?? [])
                .FirstOrDefault(x => x.System.Equals(system, StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}

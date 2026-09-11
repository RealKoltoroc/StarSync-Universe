using System.Text.Json;
using System.Text.RegularExpressions;

namespace StarSyncUniverse.Data;

public sealed record ScUnpackedEntry(
    string Uuid,
    string Name,
    string Type,
    string System,
    string? ParentUuid,
    bool Hidden,
    bool QtValid,
    double X,
    double Y,
    double Z);

public sealed class ScUnpackedCatalog
{
    public static ScUnpackedCatalog Empty { get; } = new([], "DISABLED");

    private readonly Dictionary<string, List<ScUnpackedEntry>> _byUuid;

    private ScUnpackedCatalog(List<ScUnpackedEntry> entries, string sourcePath)
    {
        Entries = entries;
        SourcePath = sourcePath;
        SourceP4 = DetectSourceP4(sourcePath);
        _byUuid = entries
            .GroupBy(x => x.Uuid, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ScUnpackedEntry> Entries { get; }
    public string SourcePath { get; }
    public int? SourceP4 { get; }

    public bool IsBuildCompatible(string build)
    {
        if (!SourceP4.HasValue) return false;
        var match = Regex.Match(build ?? string.Empty, @"(?:^|\|)P4=(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups[1].Value, out var p4) && p4 == SourceP4.Value;
    }

    public ScUnpackedEntry? FirstByUuid(string? uuid)
    {
        if (string.IsNullOrWhiteSpace(uuid)) return null;
        return _byUuid.TryGetValue(uuid, out var values) ? values[0] : null;
    }

    public ScUnpackedEntry? MatchByUuidAndWorld(
        string? uuid,
        string system,
        double x,
        double y,
        double z,
        double toleranceMeters = 1d)
    {
        if (string.IsNullOrWhiteSpace(uuid) || !_byUuid.TryGetValue(uuid, out var values))
            return null;

        ScUnpackedEntry? best = null;
        var bestDistance = double.PositiveInfinity;
        foreach (var candidate in values)
        {
            if (!candidate.System.Equals(system, StringComparison.OrdinalIgnoreCase))
                continue;

            var dx = candidate.X - x;
            var dy = candidate.Y - y;
            var dz = candidate.Z - z;
            var distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best is not null && bestDistance <= toleranceMeters ? best : null;
    }

    private static int? DetectSourceP4(string sourcePath)
    {
        var normalized = Path.GetFullPath(sourcePath).Replace('\\', '/');
        var match = Regex.Match(normalized, @"LIVE[._-](\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success && int.TryParse(match.Groups[1].Value, out var p4) ? p4 : null;
    }

    public static ScUnpackedCatalog Load(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var json = JsonDocument.Parse(stream);
        var list = new List<ScUnpackedEntry>();

        foreach (var e in json.RootElement.GetProperty("entities").EnumerateArray())
        {
            list.Add(new ScUnpackedEntry(
                Uuid: e.GetProperty("uuid").GetString() ?? string.Empty,
                Name: e.GetProperty("name").GetString() ?? "Unknown",
                Type: e.GetProperty("type").GetString() ?? "unknown",
                System: e.GetProperty("system").GetString() ?? string.Empty,
                ParentUuid: e.TryGetProperty("parent_uuid", out var p) && p.ValueKind != JsonValueKind.Null ? p.GetString() : null,
                Hidden: e.TryGetProperty("hidden", out var h) && h.GetBoolean(),
                QtValid: e.TryGetProperty("qt_valid", out var q) && q.GetBoolean(),
                X: e.GetProperty("x").GetDouble(),
                Y: e.GetProperty("y").GetDouble(),
                Z: e.GetProperty("z").GetDouble()));
        }

        return new ScUnpackedCatalog(list, Path.GetFullPath(filePath));
    }
}

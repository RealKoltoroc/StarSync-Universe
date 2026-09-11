using System.Text.RegularExpressions;

namespace StarSyncUniverse.Data;

public static partial class LocalUniverseIdentity
{
    public static string DeriveName(string system, string? entityName, string? label, string? sourcePath, string fallback)
    {
        var raw = FirstNonEmpty(entityName, label, Path.GetFileNameWithoutExtension(sourcePath ?? string.Empty), fallback);
        if (string.IsNullOrWhiteSpace(raw)) return fallback;

        var ooc = OrbitingBodyNamePattern().Match(raw);
        if (ooc.Success && ooc.Groups[1].Value.Equals(system, StringComparison.OrdinalIgnoreCase))
            return HumanizeToken(ooc.Groups[3].Value);

        var cleaned = raw;
        if (cleaned.EndsWith("_LOC", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[..^4];
        if (cleaned.StartsWith("OOC_", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[4..];

        return HumanizeToken(cleaned);
    }

    public static string InferType(string system, string? entityClass, string? sourcePath, string? entityName)
    {
        var stem = Path.GetFileNameWithoutExtension((sourcePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar));
        if (stem.Equals(system + "star", StringComparison.OrdinalIgnoreCase)) return "Star";
        if (Regex.IsMatch(stem, $"^{Regex.Escape(system)}\\d+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return "Planet";
        if (Regex.IsMatch(stem, $"^{Regex.Escape(system)}\\d+[a-z]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) return "Moon";

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            var match = OrbitingBodyNamePattern().Match(entityName);
            if (match.Success && match.Groups[1].Value.Equals(system, StringComparison.OrdinalIgnoreCase))
            {
                var ordinal = match.Groups[2].Value;
                return ordinal.Any(char.IsLetter) ? "Moon" : "Planet";
            }
        }

        return string.IsNullOrWhiteSpace(entityClass) ? "ObjectContainer" : entityClass;
    }

    public static bool IsUsableDisplayName(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        !value.Contains("UNINITIALIZED", StringComparison.OrdinalIgnoreCase);

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(IsUsableDisplayName) ?? string.Empty;

    private static string HumanizeToken(string value)
    {
        value = value.Replace('_', ' ').Trim();
        value = CamelBoundary().Replace(value, "$1 $2");
        value = Regex.Replace(value, @"\s+", " ").Trim();
        return value;
    }

    [GeneratedRegex(@"^OOC_([^_]+)_([0-9]+[A-Za-z]?)_(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OrbitingBodyNamePattern();

    [GeneratedRegex(@"([a-z0-9])([A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex CamelBoundary();
}

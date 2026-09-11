using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Data;

public sealed class AaronHaloImporter
{
    private readonly StarBreakerClient _starBreaker;

    public AaronHaloImporter(StarBreakerClient starBreaker) => _starBreaker = starBreaker;

    public async Task<SpatialRegion> ImportAsync(CancellationToken cancellationToken = default)
    {
        const string filter = "Data/ObjectContainers/PU/system/stanton/aaronhalo.socpak";
        var extractRoot = await _starBreaker.ExtractAsync(filter, "stanton-aaron-halo", cancellationToken);
        var socpak = Directory.EnumerateFiles(extractRoot, "aaronhalo.socpak", SearchOption.AllDirectories).Single();

        using var zip = ZipFile.OpenRead(socpak);
        var xmlEntry = zip.Entries.Single(e => e.FullName.Equals("aaronhalo.xml", StringComparison.OrdinalIgnoreCase));
        using var xmlReader = new StreamReader(xmlEntry.Open());
        var doc = XDocument.Load(xmlReader);

        var socEntry = zip.Entries.Single(e => e.FullName.Equals("aaronhalo.soc", StringComparison.OrdinalIgnoreCase));
        using var ms = new MemoryStream();
        using (var stream = socEntry.Open()) stream.CopyTo(ms);
        var ascii = Encoding.ASCII.GetString(ms.ToArray());

        var innerKm = ReadNumberAfterToken(ascii, "innerRadiusKm");
        var outerKm = ReadNumberAfterToken(ascii, "outerRadiusKm");
        var depthKm = ReadNumberAfterToken(ascii, "depthKm");
        var density = ReadNumberAfterToken(ascii, "densityScale");
        var composition = ReadAsciiValueAfterToken(ascii, "composition");

        var exposed = doc.Descendants("Entity").FirstOrDefault(e => (e.Attribute("label")?.Value ?? string.Empty).Contains("Aaron", StringComparison.OrdinalIgnoreCase));
        var id = exposed?.Attribute("guid")?.Value ?? "aaron-halo";

        return new SpatialRegion(
            Id: id,
            Name: "Aaron Halo",
            Type: "AsteroidBelt",
            System: "stanton",
            ParentFrame: "system:stanton",
            CenterX: 0,
            CenterY: 0,
            CenterZ: 0,
            InnerRadiusMeters: innerKm * 1000d,
            OuterRadiusMeters: outerKm * 1000d,
            ThicknessMeters: depthKm * 1000d,
            DensityScale: density,
            Composition: composition,
            SourcePath: filter,
            SourceAuthority: "Data.p4k/ObjectContainer/AsteroidRing",
            DataStatus: "LOCAL_DIRECT");
    }

    private static double ReadNumberAfterToken(string text, string token)
    {
        var match = Regex.Match(text, Regex.Escape(token) + @"[^0-9+\-.]*([+\-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+\-]?\d+)?)", RegexOptions.IgnoreCase);
        return match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0d;
    }

    private static string? ReadAsciiValueAfterToken(string text, string token)
    {
        var index = text.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return null;
        var tail = text[(index + token.Length)..];
        var matches = Regex.Matches(tail, @"[A-Za-z][A-Za-z0-9_\-]{3,}");
        return matches.Count > 0 ? matches[0].Value : null;
    }
}

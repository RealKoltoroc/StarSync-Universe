using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Data;

public sealed class DeepObjectContainerResolver
{
    private readonly StarBreakerClient _starBreaker;

    public DeepObjectContainerResolver(StarBreakerClient starBreaker) => _starBreaker = starBreaker;

    public async Task<ObjectContainerGraphResult> ResolveAsync(
        string rootSourcePath,
        int maxDepth = 1,
        int maxContainers = 32,
        CancellationToken cancellationToken = default)
    {
        if (maxDepth < 0 || maxDepth > 4) throw new ArgumentOutOfRangeException(nameof(maxDepth));
        if (maxContainers < 1 || maxContainers > 256) throw new ArgumentOutOfRangeException(nameof(maxContainers));

        var nodes = new List<ObjectContainerGraphNode>();
        var unresolved = new List<string>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolved = 0;

        async Task ResolveContainerAsync(string sourcePath, string? parentPlacementNodeId, int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (depth > maxDepth || resolved >= maxContainers) return;
            sourcePath = NormalizeSourcePath(sourcePath);
            if (!visited.Add(sourcePath)) return;

            string extractRoot;
            try
            {
                var cache = "deep-oc-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sourcePath))).Substring(0, 16).ToLowerInvariant();
                extractRoot = await _starBreaker.ExtractAsync(sourcePath, cache, cancellationToken);
            }
            catch
            {
                unresolved.Add(sourcePath);
                return;
            }

            var fileName = Path.GetFileName(sourcePath.Replace('/', Path.DirectorySeparatorChar));
            var socpak = Directory.EnumerateFiles(extractRoot, fileName, SearchOption.AllDirectories).FirstOrDefault();
            if (socpak is null)
            {
                unresolved.Add(sourcePath);
                return;
            }

            XDocument? doc = null;
            try
            {
                using var zip = ZipFile.OpenRead(socpak);
                var xmlName = Path.GetFileNameWithoutExtension(fileName) + ".xml";
                var entry = zip.Entries.FirstOrDefault(e => Path.GetFileName(e.FullName).Equals(xmlName, StringComparison.OrdinalIgnoreCase));
                if (entry is null)
                {
                    unresolved.Add(sourcePath + "#root-xml-missing");
                    return;
                }
                using var reader = new StreamReader(entry.Open());
                doc = XDocument.Load(reader);
            }
            catch (InvalidDataException)
            {
                unresolved.Add(sourcePath + "#unsupported-socpak");
                return;
            }

            resolved++;
            var children = doc.Root?.Element("ChildObjectContainers")?.Elements("Child") ?? [];
            var childIndex = 0;
            foreach (var child in children)
            {
                childIndex++;
                var id = child.Attribute("guid")?.Value ?? child.Attribute("starMapRecord")?.Value ?? $"{sourcePath}#{childIndex}";
                var nodeId = $"ocnode:{depth}:{id}";
                var pos = ParseVector(child.Attribute("pos")?.Value);
                var rot = ParseQuaternion(child.Attribute("rot")?.Value);
                var referenced = child.Attribute("name")?.Value;
                var normalizedRef = IsSocpakReference(referenced) ? NormalizeSourcePath(referenced!) : null;
                nodes.Add(new ObjectContainerGraphNode(
                    nodeId,
                    parentPlacementNodeId,
                    depth,
                    sourcePath,
                    normalizedRef,
                    child.Attribute("starMapRecord")?.Value,
                    child.Attribute("class")?.Value,
                    child.Attribute("entityName")?.Value ?? child.Attribute("label")?.Value,
                    pos.X, pos.Y, pos.Z,
                    rot.W, rot.X, rot.Y, rot.Z,
                    "RAW_CHILD_LOCAL_UNCOMPOSED",
                    "Data.p4k/ObjectContainer root XML",
                    "LOCAL_DIRECT_RAW_TRANSFORM"));

                if (normalizedRef is not null && depth < maxDepth && resolved < maxContainers)
                    await ResolveContainerAsync(normalizedRef, nodeId, depth + 1);
            }
        }

        await ResolveContainerAsync(rootSourcePath, null, 0);
        return new ObjectContainerGraphResult(
            NormalizeSourcePath(rootSourcePath),
            resolved,
            nodes,
            unresolved.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            unresolved.Count == 0 ? "LOCAL_DIRECT_GRAPH_RESOLVED" : "LOCAL_DIRECT_GRAPH_PARTIAL");
    }

    private static bool IsSocpakReference(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains(".socpak", StringComparison.OrdinalIgnoreCase) && value.StartsWith("Data/", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeSourcePath(string value)
    {
        var normalized = value.Replace('\\', '/');
        const string puPrefix = "Data/objectcontainers/pu/";
        if (normalized.StartsWith(puPrefix, StringComparison.OrdinalIgnoreCase))
            normalized = "Data/ObjectContainers/PU/" + normalized[puPrefix.Length..];
        else
        {
            const string lowerPrefix = "Data/objectcontainers/";
            if (normalized.StartsWith(lowerPrefix, StringComparison.OrdinalIgnoreCase))
                normalized = "Data/ObjectContainers/" + normalized[lowerPrefix.Length..];
        }
        return normalized;
    }

    private static (double X, double Y, double Z) ParseVector(string? value)
    {
        var p = (value ?? string.Empty).Split(',');
        return (Part(p, 0), Part(p, 1), Part(p, 2));
    }

    private static (double W, double X, double Y, double Z) ParseQuaternion(string? value)
    {
        var p = (value ?? string.Empty).Split(',');
        return (Part(p, 0, 1d), Part(p, 1), Part(p, 2), Part(p, 3));
    }

    private static double Part(string[] values, int index, double fallback = 0d) =>
        index < values.Length && double.TryParse(values[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : fallback;
}

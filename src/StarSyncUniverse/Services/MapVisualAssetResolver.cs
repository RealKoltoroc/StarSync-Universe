using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public sealed class MapVisualAssetResolver
{
    private static readonly string[] ModelExtensions = [".glb", ".gltf", ".obj"];
    private static readonly string[] IconExtensions = [".png", ".webp", ".svg"];

    private readonly string _assetsRoot;

    public MapVisualAssetResolver(string? assetsRoot = null)
    {
        _assetsRoot = string.IsNullOrWhiteSpace(assetsRoot)
            ? Path.Combine(AppContext.BaseDirectory, "Assets", "MapVisuals")
            : assetsRoot;
    }

    public MapVisualAssetDescriptor Resolve(
        string canonicalId,
        MapVisualClass visualClass,
        bool prefer3D = true)
    {
        var safeCanonical = Sanitize(canonicalId);
        var className = visualClass.ToString();

        if (prefer3D)
        {
            var exactOverride = FindFirstExisting(Path.Combine(_assetsRoot, "Overrides", "by-canonical-id", safeCanonical), ModelExtensions);
            if (exactOverride is not null)
                return Build(canonicalId, visualClass, MapVisualRepresentation.OverrideModel3D, exactOverride, true, "USER_OR_OPTIONAL_ASSET_PACK", "OVERRIDE_MODEL_AVAILABLE");

            var classOverride = FindFirstExisting(Path.Combine(_assetsRoot, "Overrides", "by-class", className), ModelExtensions);
            if (classOverride is not null)
                return Build(canonicalId, visualClass, MapVisualRepresentation.OverrideModel3D, classOverride, true, "OPTIONAL_ASSET_PACK", "CLASS_OVERRIDE_MODEL_AVAILABLE");

            var standardModel = FindFirstExisting(Path.Combine(_assetsRoot, "Standard", "Models", className), ModelExtensions);
            if (standardModel is not null)
                return Build(canonicalId, visualClass, MapVisualRepresentation.StandardModel3D, standardModel, false, "STARSYNC_STANDARD_ASSET", "STANDARD_MODEL_AVAILABLE");
        }

        var standardIcon = FindFirstExisting(Path.Combine(_assetsRoot, "Standard", "Icons", className), IconExtensions);
        if (standardIcon is not null)
            return Build(canonicalId, visualClass, MapVisualRepresentation.Icon2D, standardIcon, false, "STARSYNC_STANDARD_ASSET", "STANDARD_ICON_AVAILABLE");

        return new MapVisualAssetDescriptor(
            canonicalId,
            visualClass,
            MapVisualRepresentation.Icon2D,
            $"builtin://map-visual/{className}",
            true,
            false,
            "STARSYNC_BUILTIN_FALLBACK",
            "BUILTIN_ICON_FALLBACK");
    }

    public static MapVisualClass Classify(string? type, string? name, string? entityClass, string? sourcePath)
    {
        var text = string.Join(' ', type, name, entityClass, sourcePath).ToLowerInvariant();
        if (text.Contains("jump") && (text.Contains("point") || text.Contains("gate"))) return MapVisualClass.JumpPoint;
        if (text.Contains("rctrk") || text.Contains("racetrack") || text.Contains("racing static") || text.Contains("racing_static")) return MapVisualClass.RacingTrack;
        if (text.Contains("commarray") || text.Contains("comm array")) return MapVisualClass.CommArray;
        if (text.Contains("reststop") || text.Contains("rest stop") || text.Contains("lagrange")) return MapVisualClass.RestStop;
        if (text.Contains("station") || text.Contains("levski")) return MapVisualClass.Station;
        if (text.Contains("landingzone") || text.Contains("landing zone")) return MapVisualClass.LandingZone;
        if (text.Contains("city") || text.Contains("area18") || text.Contains("lorville") || text.Contains("new babbage") || text.Contains("orison")) return MapVisualClass.City;
        if (text.Contains("mining")) return MapVisualClass.MiningBase;
        if (text.Contains("research")) return MapVisualClass.ResearchBase;
        if (text.Contains("bunker")) return MapVisualClass.Bunker;
        if (text.Contains("cave")) return MapVisualClass.Cave;
        if (text.Contains("settlement") || text.Contains("homestead")) return MapVisualClass.Settlement;
        if (text.Contains("outpost")) return MapVisualClass.Outpost;
        if (text.Contains("facility")) return MapVisualClass.IndustrialFacility;
        if (text.Contains("planet") || text.Contains("moon")) return MapVisualClass.CelestialBody;
        return MapVisualClass.SurfacePoi;
    }

    private static MapVisualAssetDescriptor Build(
        string key,
        MapVisualClass visualClass,
        MapVisualRepresentation representation,
        string path,
        bool isOverride,
        string authority,
        string status) =>
        new(key, visualClass, representation, path, true, isOverride, authority, status);

    private static string? FindFirstExisting(string pathWithoutExtension, IReadOnlyList<string> extensions)
    {
        foreach (var extension in extensions)
        {
            var path = pathWithoutExtension + extension;
            if (File.Exists(path)) return path;
        }

        return null;
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}

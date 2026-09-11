using System.Globalization;
using System.Xml.Linq;
using System.Windows.Media.Imaging;
using StarSyncUniverse.Data;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public sealed record LiveSurfaceTextureAsset(
    string BodyName,
    string BodyContainerName,
    string SourceDdsPath,
    string AssetPath,
    string BrowserRelativePath,
    int? PixelWidth,
    int? PixelHeight,
    string Projection,
    string SourceAuthority,
    string DataStatus,
    string AssetKind,
    string SelectionReason,
    string? CloudAssetPath = null,
    string? CloudBrowserRelativePath = null,
    int? CloudPixelWidth = null,
    int? CloudPixelHeight = null,
    string CloudAssetKind = "NONE",
    double AtmosphereStrength = 0.0,
    double AtmosphereExtent = 1.0,
    double AtmosphereR = 0.42,
    double AtmosphereG = 0.78,
    double AtmosphereB = 1.0,
    double CloudOpacity = 0.0,
    double ReliefStrength = 0.0,
    string? NormalAssetPath = null,
    string? NormalBrowserRelativePath = null,
    int? NormalPixelWidth = null,
    int? NormalPixelHeight = null,
    string NormalAssetKind = "NONE",
    double NormalStrength = 0.0,
    string? OverviewAssetPath = null,
    string? OverviewBrowserRelativePath = null,
    int? OverviewPixelWidth = null,
    int? OverviewPixelHeight = null);

public sealed record LiveSurfaceTextureCacheResult(
    string? RootPath,
    IReadOnlyDictionary<string, LiveSurfaceTextureAsset> AssetsByBodyName,
    string DataStatus);

/// <summary>
/// Resolves presentation-safe planet/moon textures from CURRENT LIVE Data.p4k.
///
/// Priority:
/// 1. body-specific *_global_starmap_diff or *_starmap_diff (presentation texture),
/// 2. a locally derived restrained presentation texture from climate+splat control maps,
/// 3. no texture (renderer fallback).
///
/// Technical climate/splat/cloud/normal/height maps are never exposed directly as visible RGB
/// surface textures. The derived path deliberately converts control channels into tone/structure
/// and is marked presentation-only/non-authoritative. Geometry and BodyFixed POIs are untouched.
/// </summary>
public static class LiveSurfaceTextureCache
{
    private const string DerivedAlgorithmVersion = "body-presentation-v17-pyro-reference-color-calibrated-all-rocky-bodies";

    public static LiveSurfaceTextureCacheResult LoadExisting(
        string system,
        IReadOnlyList<CelestialBodyPhysical> bodies)
    {
        system = system.Trim().ToLowerInvariant();
        if (bodies.Count == 0)
            return Empty("CACHED_BODY_PRESENTATION_NO_BODIES");

        var finalRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StarSyncUniverse", "Extracts", $"{system}-body-presentation-v2");
        EnsureBundledBaselineInstalled(system, finalRoot);
        if (!Directory.Exists(finalRoot))
            return Empty("BODY_PRESENTATION_BASELINE_MISSING");

        var visualAudit = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var visualAuditPath = Path.Combine(finalRoot, "body-visual-audit.tsv");
        if (File.Exists(visualAuditPath))
        {
            foreach (var line in File.ReadLines(visualAuditPath).Skip(1))
            {
                var parts = line.Split('\t');
                if (parts.Length >= 6 && !string.IsNullOrWhiteSpace(parts[0]))
                    visualAudit[parts[0]] = parts;
            }
        }

        var layerAudit = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var layerAuditPath = Path.Combine(finalRoot, "body-render-layers.tsv");
        if (File.Exists(layerAuditPath))
        {
            foreach (var line in File.ReadLines(layerAuditPath).Skip(1))
            {
                var parts = line.Split('\t');
                if (parts.Length >= 9 && !string.IsNullOrWhiteSpace(parts[0]))
                    layerAudit[parts[0]] = parts;
            }
        }

        static double ParseDouble(string[]? parts, int index, double fallback) =>
            parts is not null && parts.Length > index &&
            double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : fallback;

        var result = new Dictionary<string, LiveSurfaceTextureAsset>(StringComparer.OrdinalIgnoreCase);
        foreach (var body in bodies)
        {
            if (string.IsNullOrWhiteSpace(body.ContainerName)) continue;
            var container = body.ContainerName.Trim().ToLowerInvariant();
            var basePath = Path.Combine(finalRoot, container + "_presentation.png");
            if (!File.Exists(basePath)) continue;

            visualAudit.TryGetValue(body.Name, out var visual);
            layerAudit.TryGetValue(body.Name, out var layers);
            var strategy = visual is not null && visual.Length > 2 ? visual[2] : string.Empty;
            var source = visual is not null && visual.Length > 3 ? visual[3] : string.Empty;
            var assetKind = strategy switch
            {
                "DIRECT_STARMAP_DIFF" => "CURRENT_LIVE_DATA_P4K_STARMAP_DIFF",
                "DERIVED_TERRAIN_PRESENTATION" => "CURRENT_LIVE_DATA_P4K_DERIVED_TERRAIN_PRESENTATION",
                "GAS_GIANT_PRESENTATION" => "CURRENT_LIVE_DATA_P4K_DERIVED_GAS_GIANT_PRESENTATION",
                _ => layers is not null && layers.Length > 1 && !string.IsNullOrWhiteSpace(layers[1])
                    ? layers[1]
                    : "CACHED_BODY_PRESENTATION"
            };

            var cloudPath = Path.Combine(finalRoot, container + "_clouds.png");
            if (!File.Exists(cloudPath)) cloudPath = null;
            var normalPath = Path.Combine(finalRoot, container + "_normal.png");
            if (!File.Exists(normalPath)) normalPath = null;
            var overviewPath = Path.Combine(finalRoot, container + "_overview.png");
            if (!File.Exists(overviewPath))
            {
                EnsureOverviewTexture(basePath, overviewPath);
                if (!File.Exists(overviewPath)) overviewPath = null;
            }
            var baseSize = ReadPixelSize(basePath);
            var cloudSize = cloudPath is null ? (Width: (int?)null, Height: (int?)null) : ReadPixelSize(cloudPath);
            var normalSize = normalPath is null ? (Width: (int?)null, Height: (int?)null) : ReadPixelSize(normalPath);
            var overviewSize = overviewPath is null ? (Width: (int?)null, Height: (int?)null) : ReadPixelSize(overviewPath);
            var profile = PyroReferenceColorProfiles.Find(container);
            var atmosphereTint = profile is not null
                ? (R: profile.Light.R, G: profile.Light.G, B: profile.Light.B)
                : (R: 0.42, G: 0.78, B: 1.0);

            result[body.Name] = new LiveSurfaceTextureAsset(
                body.Name,
                body.ContainerName,
                string.IsNullOrWhiteSpace(source) ? "LOCAL_CACHED_PRESENTATION" : source,
                basePath,
                Path.GetFileName(basePath),
                baseSize.Width,
                baseSize.Height,
                "BUNDLED_OR_CACHED_EQUIRECTANGULAR_PRESENTATION",
                "Packaged StarSyncUniverse presentation baseline or locally refreshed presentation texture",
                "PRESENTATION_BASELINE_GEOMETRY_PRESERVED",
                assetKind,
                "Presentation layer is independent of data mode; no StarBreaker or Data.p4k access is required to display it.",
                cloudPath,
                cloudPath is null ? null : Path.GetFileName(cloudPath),
                cloudSize.Width,
                cloudSize.Height,
                layers is not null && layers.Length > 2 ? layers[2] : cloudPath is null ? "NONE" : "CACHED_CLOUD_LAYER",
                ParseDouble(layers, 4, 0.065),
                ParseDouble(layers, 5, 1.024),
                atmosphereTint.R,
                atmosphereTint.G,
                atmosphereTint.B,
                ParseDouble(layers, 6, 0.0),
                ParseDouble(layers, 7, 0.0),
                normalPath,
                normalPath is null ? null : Path.GetFileName(normalPath),
                normalSize.Width,
                normalSize.Height,
                layers is not null && layers.Length > 3 ? layers[3] : normalPath is null ? "NONE" : "CACHED_NORMAL_LAYER",
                ParseDouble(layers, 8, 0.0),
                overviewPath,
                overviewPath is null ? null : Path.GetFileName(overviewPath),
                overviewSize.Width,
                overviewSize.Height);
        }

        return new LiveSurfaceTextureCacheResult(
            finalRoot,
            result,
            result.Count == bodies.Count
                ? "BUNDLED_OR_CACHED_BODY_PRESENTATION_READY_ALL_BODIES"
                : result.Count > 0
                    ? "BUNDLED_OR_CACHED_BODY_PRESENTATION_PARTIAL"
                    : "BUNDLED_OR_CACHED_BODY_PRESENTATION_EMPTY");
    }

    private static void EnsureBundledBaselineInstalled(string system, string finalRoot)
    {
        var bundledRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "BodyTextures", system);
        if (!Directory.Exists(bundledRoot))
            return;

        Directory.CreateDirectory(finalRoot);
        foreach (var source in Directory.EnumerateFiles(bundledRoot, "*", SearchOption.TopDirectoryOnly))
        {
            var destination = Path.Combine(finalRoot, Path.GetFileName(source));
            CopyIfChanged(source, destination);
        }
    }

    public static LiveSurfaceTextureCacheResult PreferRefreshed(
        LiveSurfaceTextureCacheResult existing,
        LiveSurfaceTextureCacheResult refreshed,
        int expectedBodyCount)
    {
        if (existing.AssetsByBodyName.Count == 0)
            return refreshed;
        if (refreshed.AssetsByBodyName.Count == 0)
            return existing with { DataStatus = existing.DataStatus + "+LIVE_REFRESH_EMPTY_USING_EXISTING_PRESENTATION" };

        var rootPath = !string.IsNullOrWhiteSpace(refreshed.RootPath)
            ? refreshed.RootPath
            : existing.RootPath;
        var merged = new Dictionary<string, LiveSurfaceTextureAsset>(existing.AssetsByBodyName, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in refreshed.AssetsByBodyName)
            merged[pair.Key] = pair.Value;

        return new LiveSurfaceTextureCacheResult(
            rootPath,
            merged,
            merged.Count >= expectedBodyCount
                ? "BODY_PRESENTATION_READY_ALL_BODIES_INDEPENDENT_OF_DATA_MODE"
                : "BODY_PRESENTATION_PARTIAL_INDEPENDENT_OF_DATA_MODE");
    }

    public static async Task<LiveSurfaceTextureCacheResult> PrepareAsync(
        string system,
        IReadOnlyList<CelestialBodyPhysical> bodies,
        StarBreakerClient starBreaker,
        CancellationToken cancellationToken = default)
    {
        system = system.Trim().ToLowerInvariant();
        if (!starBreaker.IsAvailable || bodies.Count == 0)
            return Empty("LIVE_SURFACE_TEXTURE_SOURCE_UNAVAILABLE");

        var finalRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StarSyncUniverse", "Extracts", $"{system}-body-presentation-v2");
        Directory.CreateDirectory(finalRoot);

        var result = new Dictionary<string, LiveSurfaceTextureAsset>(StringComparer.OrdinalIgnoreCase);
        var audit = new List<string>
        {
            "BodyName\tContainer\tStrategy\tSource\tColorChart\tStatus"
        };

        var starmapRoot = await TryExtractAsync(
            starBreaker,
            $"Data/Textures/planets/global/{system}/**/*_starmap_diff.dds",
            $"{system}-starmap-diff-textures-v2",
            "dds-png",
            cancellationToken);

        // First pass: use only true body-specific starmap diffuse assets.
        foreach (var body in bodies)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(body.ContainerName)) continue;
            var container = body.ContainerName.Trim().ToLowerInvariant();
            var direct = FindDirectStarmap(starmapRoot, system, container);
            if (direct is null) continue;

            var final = Path.Combine(finalRoot, container + "_presentation.png");
            CopyIfChanged(direct.Value.PngPath, final);
            var (width, height) = ReadPixelSize(final);
            result[body.Name] = new LiveSurfaceTextureAsset(
                body.Name,
                body.ContainerName,
                direct.Value.SourceDdsPath,
                final,
                Path.GetFileName(final),
                width,
                height,
                "STAR_CITIZEN_STARMAP_DIFF",
                "CURRENT LIVE Data.p4k body-specific starmap diffuse decoded locally via StarBreaker",
                "LOCAL_DIRECT_PRESENTATION_TEXTURE_AXIS_MAPPING_DIAGNOSTIC",
                "CURRENT_LIVE_DATA_P4K_STARMAP_DIFF",
                "Exact body-container starmap diffuse; technical control maps not used as RGB surface color.");
            audit.Add($"{body.Name}\t{container}\tDIRECT_STARMAP_DIFF\t{direct.Value.SourceDdsPath}\t-\tOK");
        }

        var missing = bodies
            .Where(b => !result.ContainsKey(b.Name) && !string.IsNullOrWhiteSpace(b.ContainerName))
            .ToArray();

        // A system may not ship dedicated starmap diffuse textures (CURRENT LIVE Pyro does not).
        // In that case build a safe presentation preview from terrain controls instead of showing
        // climate/cloud/control RGB channels directly.
        if (missing.Length > 0)
        {
            var climateRoot = await TryExtractAsync(
                starBreaker,
                $"Data/Textures/planets/global/{system}/**/*_clim.dds",
                $"{system}-terrain-climate-controls-v2",
                "dds-png",
                cancellationToken);
            var splatRoot = await TryExtractAsync(
                starBreaker,
                $"Data/Textures/planets/global/{system}/**/*_splat.dds",
                $"{system}-terrain-splat-controls-v2",
                "dds-png",
                cancellationToken);
            var elevationRoot = await TryExtractAsync(
                starBreaker,
                $"Data/Textures/planets/global/{system}/**/*_elev.dds",
                $"{system}-terrain-elevation-controls-v4",
                "dds-png",
                cancellationToken);
            var displacementRoot = await TryExtractAsync(
                starBreaker,
                $"Data/Textures/planets/global/{system}/**/*_displ.dds",
                $"{system}-terrain-displacement-controls-v2",
                "dds-png",
                cancellationToken);
            var materialRoot = await TryExtractAsync(
                starBreaker,
                $"Data/Textures/planets/global/{system}/**/*.mtl",
                $"{system}-planet-materials-v4",
                "cryxml",
                cancellationToken);
            var chartSystem = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(system);
            var chartRoot = await TryExtractAsync(
                starBreaker,
                $"Data/Textures/colorcharts/Systems/{chartSystem}/**/*cch*.dds",
                $"{system}-body-colorcharts-v2",
                "dds-png",
                cancellationToken);
            var gasCloudRoot = await TryExtractAsync(
                starBreaker,
                $"Data/Textures/planets/global/{system}/**/*_cloud_global.dds",
                $"{system}-gas-giant-cloud-controls-v2",
                "dds-png",
                cancellationToken);
            var gasTintRoot = await TryExtractAsync(
                starBreaker,
                $"Data/Textures/planets/global/{system}/**/*_cloud_tint_gradient.dds",
                $"{system}-gas-giant-tint-gradients-v2",
                "dds-png",
                cancellationToken);

            foreach (var body in missing)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var container = body.ContainerName.Trim().ToLowerInvariant();
                var climate = FindControlTexture(climateRoot, system, container, "clim");
                var splat = FindControlTexture(splatRoot, system, container, "splat");
                var elevation = FindControlTexture(elevationRoot, system, container, "elev");
                var displacement = FindControlTexture(displacementRoot, system, container, "displ");
                var materialHints = ReadMaterialHints(materialRoot, system, container);
                var chart = FindBestColorChart(chartRoot, system, container);
                var gasCloud = FindControlTexture(gasCloudRoot, system, container, "cloud_global");
                var gasTint = FindControlTexture(gasTintRoot, system, container, "cloud_tint_gradient");
                var final = Path.Combine(finalRoot, container + "_presentation.png");

                if ((climate is null || splat is null) && gasCloud is not null && gasTint is not null)
                {
                    if (NeedsRebuild(final, gasCloud, gasTint, chart) &&
                        !BodyPresentationTextureBuilder.TryBuildGasGiant(gasCloud, gasTint, chart, container, final))
                    {
                        audit.Add($"{body.Name}\t{container}\tGAS_GIANT_PRESENTATION\t{gasCloud};{gasTint}\t{Path.GetFileName(chart) ?? "-"}\tBUILD_FAILED");
                        continue;
                    }

                    var (gasWidth, gasHeight) = ReadPixelSize(final);
                    result[body.Name] = new LiveSurfaceTextureAsset(
                        body.Name,
                        body.ContainerName,
                        $"Data/Textures/planets/global/{system}/{container}/{container}_cloud_global.dds",
                        final,
                        Path.GetFileName(final),
                        gasWidth,
                        gasHeight,
                        "DERIVED_GAS_GIANT_PRESENTATION",
                        "CURRENT LIVE Data.p4k gas-giant cloud field plus body tint gradient transformed to presentation color",
                        "DERIVED_PRESENTATION_ONLY_GAS_GIANT_NOT_AUTHORITATIVE_ALBEDO_AXIS_MAPPING_DIAGNOSTIC",
                        "CURRENT_LIVE_DATA_P4K_DERIVED_GAS_GIANT_PRESENTATION",
                        "Gas-giant source detected from body cloud field+tint gradient; cloud control channels are converted through the supplied tint instead of displayed as false-color RGB.");
                    audit.Add($"{body.Name}\t{container}\tGAS_GIANT_PRESENTATION\t{Path.GetFileName(gasCloud)}+{Path.GetFileName(gasTint)}\t{Path.GetFileName(chart) ?? "-"}\tOK");
                    continue;
                }

                if (climate is null || splat is null)
                {
                    audit.Add($"{body.Name}\t{container}\tNONE\t-\t{Path.GetFileName(chart) ?? "-"}\tMISSING_PRESENTATION_SOURCE");
                    continue;
                }

                if (NeedsRebuild(final, climate, splat, elevation, displacement, chart))
                {
                    if (!BodyPresentationTextureBuilder.TryBuild(climate, splat, elevation, displacement, materialHints, chart, container, final))
                    {
                        audit.Add($"{body.Name}\t{container}\tDERIVED_TERRAIN_PRESENTATION\t{climate};{splat};{elevation};{displacement}\t{Path.GetFileName(chart) ?? "-"}\tBUILD_FAILED");
                        continue;
                    }
                }

                var (width, height) = ReadPixelSize(final);
                var climateSource = $"Data/Textures/planets/global/{system}/{container}/{Path.GetFileNameWithoutExtension(climate)}.dds";
                result[body.Name] = new LiveSurfaceTextureAsset(
                    body.Name,
                    body.ContainerName,
                    climateSource,
                    final,
                    Path.GetFileName(final),
                    width,
                    height,
                    "DERIVED_TERRAIN_PRESENTATION",
                    "CURRENT LIVE Data.p4k splat+displacement surface reconstruction with weak climate/elevation support; optional CURRENT LIVE body CCH grade",
                    "DERIVED_PRESENTATION_ONLY_NOT_AUTHORITATIVE_ALBEDO_AXIS_MAPPING_DIAGNOSTIC",
                    "CURRENT_LIVE_DATA_P4K_DERIVED_TERRAIN_PRESENTATION",
                    "No body-specific starmap diffuse exists; technical control maps converted to tone/structure and never rendered directly as RGB.");
                audit.Add($"{body.Name}\t{container}\tDERIVED_TERRAIN_PRESENTATION\t{Path.GetFileName(climate)}+{Path.GetFileName(splat)}\t{Path.GetFileName(chart) ?? "-"}\tOK");
            }
        }

        // Presentation-quality auxiliary layers: use only explicit global cloud fields as cloud coverage.
        // The cloud RGB values are never treated as final color in the renderer; only their coverage/luminance
        // drives a softly tinted cloud/haze layer. Bodies without such a field still receive only a restrained
        // presentation rim, never an invented geographic cloud pattern.
        var cloudsPluralRoot = await TryExtractAsync(
            starBreaker,
            $"Data/Textures/planets/global/{system}/**/*_clouds_global.dds",
            $"{system}-body-clouds-global-v3",
            "dds-png",
            cancellationToken);
        var cloudSingularRoot = await TryExtractAsync(
            starBreaker,
            $"Data/Textures/planets/global/{system}/**/*_cloud_global.dds",
            $"{system}-body-cloud-global-v3",
            "dds-png",
            cancellationToken);
        var normalRoot = await TryExtractAsync(
            starBreaker,
            $"Data/Textures/planets/global/{system}/**/*_ddn.dds",
            $"{system}-body-normal-ddn-v4",
            "dds-png",
            cancellationToken);

        foreach (var bodyName in result.Keys.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var asset = result[bodyName];
            var container = asset.BodyContainerName.Trim().ToLowerInvariant();
            var cloud = FindCloudTexture(cloudsPluralRoot, cloudSingularRoot, system, container);
            string? cloudFinal = null;
            int? cloudWidth = null;
            int? cloudHeight = null;
            var cloudKind = "NONE";
            string? normalFinal = null;
            int? normalWidth = null;
            int? normalHeight = null;
            var normalKind = "NONE";
            var normalSource = FindControlTexture(normalRoot, system, container, "ddn");
            if (normalSource is not null)
            {
                normalFinal = Path.Combine(finalRoot, container + "_normal.png");
                CopyIfChanged(normalSource, normalFinal);
                var normalSize = ReadPixelSize(normalFinal);
                normalWidth = normalSize.Width;
                normalHeight = normalSize.Height;
                normalKind = "CURRENT_LIVE_GLOBAL_DDN_NORMAL";
            }

            // Gas giants already use their cloud field as the base presentation texture.
            if (cloud is not null && !asset.AssetKind.Contains("GAS_GIANT", StringComparison.OrdinalIgnoreCase))
            {
                cloudFinal = Path.Combine(finalRoot, container + "_clouds.png");
                CopyIfChanged(cloud, cloudFinal);
                var cloudSize = ReadPixelSize(cloudFinal);
                cloudWidth = cloudSize.Width;
                cloudHeight = cloudSize.Height;
                cloudKind = "CURRENT_LIVE_GLOBAL_CLOUD_COVERAGE";
            }            var derived = asset.AssetKind.Contains("DERIVED_TERRAIN", StringComparison.OrdinalIgnoreCase);
            var gasGiant = asset.AssetKind.Contains("GAS_GIANT", StringComparison.OrdinalIgnoreCase);
            var visualReference = PyroReferenceColorProfiles.Find(container);
            var referenceRocky = visualReference is { GasGiant: false };
            var hasCloud = cloudFinal is not null;
            var atmosphereStrength = visualReference is not null
                ? (container is "pyro1" or "pyro3" ? 0.13 : gasGiant ? 0.12 : 0.075)
                : gasGiant ? 0.18 : hasCloud ? 0.16 : derived ? 0.085 : 0.065;
            var atmosphereExtent = gasGiant ? 1.045 : hasCloud ? 1.040 : derived ? 1.030 : 1.024;
            var cloudOpacity = hasCloud ? (derived ? 0.16 : 0.20) : 0.0;
            // Reference profiles can explicitly require a cloud-free map presentation. The source cloud maps
            // remain archived and auditable, but are not composited into those bodies.
            if (visualReference?.ForceCloudFree == true)
                cloudOpacity = 0.0;
            var terminusRockyReference = container.Equals("pyro6", StringComparison.OrdinalIgnoreCase);
            var reliefStrength = gasGiant ? 0.05 : terminusRockyReference ? 0.04 : referenceRocky ? 0.10 : 0.14;
            var atmosphereTint = visualReference is not null
                ? (R: visualReference.Light.R, G: visualReference.Light.G, B: visualReference.Light.B)
                : derived
                    ? (R: 0.78, G: 0.69, B: 0.58)
                    : (R: 0.42, G: 0.78, B: 1.00);

            result[bodyName] = asset with
            {
                CloudAssetPath = cloudFinal,
                CloudBrowserRelativePath = cloudFinal is null ? null : Path.GetFileName(cloudFinal),
                CloudPixelWidth = cloudWidth,
                CloudPixelHeight = cloudHeight,
                CloudAssetKind = cloudKind,
                AtmosphereStrength = atmosphereStrength,
                AtmosphereExtent = atmosphereExtent,
                AtmosphereR = atmosphereTint.R,
                AtmosphereG = atmosphereTint.G,
                AtmosphereB = atmosphereTint.B,
                CloudOpacity = cloudOpacity,
                ReliefStrength = reliefStrength,
                NormalAssetPath = normalFinal,
                NormalBrowserRelativePath = normalFinal is null ? null : Path.GetFileName(normalFinal),
                NormalPixelWidth = normalWidth,
                NormalPixelHeight = normalHeight,
                NormalAssetKind = normalKind,
                NormalStrength = normalFinal is null ? 0.0 : gasGiant ? 0.05 : terminusRockyReference ? 0.04 : referenceRocky ? 0.16 : derived ? 0.24 : 0.22
            };
        }

        await File.WriteAllLinesAsync(Path.Combine(finalRoot, "body-visual-audit.tsv"), audit, cancellationToken);
        if (system.Equals("pyro", StringComparison.OrdinalIgnoreCase))
        {
            static string RgbText(PresentationRgb color) => string.Join(',',
                color.R.ToString("0.###", CultureInfo.InvariantCulture),
                color.G.ToString("0.###", CultureInfo.InvariantCulture),
                color.B.ToString("0.###", CultureInfo.InvariantCulture));
            var referenceAudit = new List<string>
            {
                "Container\tBodyName\tDarkRGB\tMidRGB\tLightRGB\tWarmRGB\tCoolRGB\tMeanRGB\tGasGiant\tForceCloudFree\tReference"
            };
            referenceAudit.AddRange(PyroReferenceColorProfiles.All
                .OrderBy(x => x.Container, StringComparer.OrdinalIgnoreCase)
                .Select(x => string.Join('\t',
                    x.Container,
                    x.BodyName,
                    RgbText(x.Dark),
                    RgbText(x.Mid),
                    RgbText(x.Light),
                    RgbText(x.Warm),
                    RgbText(x.Cool),
                    RgbText(x.Mean),
                    x.GasGiant,
                    x.ForceCloudFree,
                    x.ReferenceUrl)));
            await File.WriteAllLinesAsync(Path.Combine(finalRoot, "body-reference-colors.tsv"), referenceAudit, cancellationToken);
        }        var renderLayerAudit = new List<string>
        {
            "BodyName\tBaseAssetKind\tCloudAssetKind\tNormalAssetKind\tAtmosphereStrength\tAtmosphereExtent\tCloudOpacity\tReliefStrength\tNormalStrength"
        };
        renderLayerAudit.AddRange(result.Values
            .OrderBy(x => x.BodyName, StringComparer.OrdinalIgnoreCase)
            .Select(x => string.Join('\t',
                x.BodyName,
                x.AssetKind,
                x.CloudAssetKind,
                x.NormalAssetKind,
                x.AtmosphereStrength.ToString("0.###", CultureInfo.InvariantCulture),
                x.AtmosphereExtent.ToString("0.###", CultureInfo.InvariantCulture),
                x.CloudOpacity.ToString("0.###", CultureInfo.InvariantCulture),
                x.ReliefStrength.ToString("0.###", CultureInfo.InvariantCulture),
                x.NormalStrength.ToString("0.###", CultureInfo.InvariantCulture))));
        await File.WriteAllLinesAsync(Path.Combine(finalRoot, "body-render-layers.tsv"), renderLayerAudit, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(finalRoot, ".algorithm-version"), DerivedAlgorithmVersion, cancellationToken);

        foreach (var bodyName in result.Keys.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var asset = result[bodyName];
            var overviewPath = Path.Combine(finalRoot, asset.BodyContainerName.Trim().ToLowerInvariant() + "_overview.png");
            EnsureOverviewTexture(asset.AssetPath, overviewPath);
            if (!File.Exists(overviewPath)) continue;
            var overviewSize = ReadPixelSize(overviewPath);
            result[bodyName] = asset with
            {
                OverviewAssetPath = overviewPath,
                OverviewBrowserRelativePath = Path.GetFileName(overviewPath),
                OverviewPixelWidth = overviewSize.Width,
                OverviewPixelHeight = overviewSize.Height
            };
        }

        return new LiveSurfaceTextureCacheResult(
            finalRoot,
            result,
            result.Count == bodies.Count
                ? "CURRENT_LIVE_BODY_PRESENTATION_TEXTURE_CACHE_READY_ALL_BODIES"
                : result.Count > 0
                    ? "CURRENT_LIVE_BODY_PRESENTATION_TEXTURE_CACHE_PARTIAL"
                    : "NO_PRESENTATION_SAFE_BODY_TEXTURE_MATCH");
    }

    private static async Task<string?> TryExtractAsync(
        StarBreakerClient starBreaker,
        string filter,
        string cacheName,
        string convert,
        CancellationToken cancellationToken)
    {
        try
        {
            return await starBreaker.ExtractAsync(filter, cacheName, cancellationToken, convert);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static (string PngPath, string SourceDdsPath)? FindDirectStarmap(
        string? root,
        string system,
        string container)
    {
        if (string.IsNullOrWhiteSpace(root)) return null;
        var folder = Path.Combine(root, "Data", "Textures", "planets", "global", system, container);
        if (!Directory.Exists(folder)) return null;

        foreach (var stem in new[]
                 {
                     container + "_global_starmap_diff",
                     container + "_starmap_diff"
                 })
        {
            var png = Path.Combine(folder, stem + ".png");
            if (!File.Exists(png)) continue;
            return (
                png,
                $"Data/Textures/planets/global/{system}/{container}/{stem}.dds");
        }

        return null;
    }

    private static string? FindControlTexture(string? root, string system, string container, string kind)
    {
        if (string.IsNullOrWhiteSpace(root)) return null;
        var folder = Path.Combine(root, "Data", "Textures", "planets", "global", system, container);
        if (!Directory.Exists(folder)) return null;
        foreach (var stem in new[] { container + "_global_" + kind, container + "_" + kind })
        {
            var path = Path.Combine(folder, stem + ".png");
            if (File.Exists(path)) return path;
        }
        return null;
    }

    private static string? FindCloudTexture(string? pluralRoot, string? singularRoot, string system, string container)
    {
        foreach (var candidate in new[]
                 {
                     (Root: pluralRoot, Stem: container + "_clouds_global"),
                     (Root: singularRoot, Stem: container + "_cloud_global")
                 })
        {
            if (string.IsNullOrWhiteSpace(candidate.Root)) continue;
            var folder = Path.Combine(candidate.Root, "Data", "Textures", "planets", "global", system, container);
            if (!Directory.Exists(folder)) continue;
            var direct = Path.Combine(folder, candidate.Stem + ".png");
            if (File.Exists(direct)) return direct;
        }
        return null;
    }

    private static string? FindBestColorChart(string? root, string system, string container)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return null;
        var files = Directory.EnumerateFiles(root, "*.png", SearchOption.AllDirectories).ToArray();
        if (files.Length == 0) return null;

        var suffix = container.StartsWith(system, StringComparison.OrdinalIgnoreCase)
            ? container[system.Length..]
            : container;
        var token = suffix.Trim('_', '-', ' ');

        return files
            .Select(path => (Path: path, Score: ScoreColorChart(Path.GetFileNameWithoutExtension(path), system, container, token)))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Path)
            .FirstOrDefault();
    }

    private static int ScoreColorChart(string name, string system, string container, string token)
    {
        name = name.ToLowerInvariant();
        system = system.ToLowerInvariant();
        container = container.ToLowerInvariant();
        token = token.ToLowerInvariant();
        var score = 0;
        if (name.Equals(container + "_cch", StringComparison.Ordinal)) score = Math.Max(score, 100);
        if (!string.IsNullOrEmpty(token) && name.Contains(token + "_" + system + "_orbit_cch", StringComparison.Ordinal)) score = Math.Max(score, 98);
        if (!string.IsNullOrEmpty(token) && name.Contains(token + "_" + system + "_cch", StringComparison.Ordinal)) score = Math.Max(score, 94);
        if (name.Contains(container, StringComparison.Ordinal) && name.Contains("cch", StringComparison.Ordinal)) score = Math.Max(score, 90);
        if (!string.IsNullOrEmpty(token) && name.Contains(token, StringComparison.Ordinal) && name.Contains("cch", StringComparison.Ordinal)) score = Math.Max(score, 72);
        return score;
    }

    private static BodyPresentationMaterialHints? ReadMaterialHints(string? root, string system, string container)
    {
        if (string.IsNullOrWhiteSpace(root)) return null;
        var folder = Path.Combine(root, "Data", "Textures", "planets", "global", system, container);
        if (!Directory.Exists(folder)) return null;

        var terrainCandidates = new[]
        {
            Path.Combine(folder, container + ".mtl"),
            Path.Combine(folder, container + "_planet.mtl"),
            Path.Combine(folder, container + "_planet_v5.mtl")
        };
        var terrainPath = terrainCandidates.FirstOrDefault(File.Exists);
        var oceanPath = Path.Combine(folder, container + "_ocean.mtl");

        (double? R, double? G, double? B) wet = default;
        (double? R, double? G, double? B) ocean = default;
        (double? R, double? G, double? B) shore = default;
        (double? R, double? G, double? B) variation = default;

        try
        {
            if (terrainPath is not null)
            {
                var doc = XDocument.Load(terrainPath);
                var publicParams = doc.Root?.Element("PublicParams");
                wet = ParseColor(publicParams?.Attribute("WetEdgeColor")?.Value);
            }
            if (File.Exists(oceanPath))
            {
                var doc = XDocument.Load(oceanPath);
                var publicParams = doc.Root?.Element("PublicParams");
                var shader = doc.Root?.Attribute("Shader")?.Value;
                if (string.Equals(shader, "FrozenOcean", StringComparison.OrdinalIgnoreCase))
                {
                    // FrozenOcean uses DistanceOceanColor as the actual long-range visible ice/water cue.
                    // Do not feed CoastTransitionColor into the generic warm shoreline slot: it is a cool
                    // transition color on Terminus and previously desaturated the entire land palette.
                    ocean = ParseColor(publicParams?.Attribute("DistanceOceanColor")?.Value);
                    if (ocean.R is null)
                        ocean = ParseColor(doc.Root?.Attribute("Diffuse")?.Value);
                }
                else
                {
                    ocean = ParseColor(doc.Root?.Attribute("Diffuse")?.Value);
                    shore = ParseColor(publicParams?.Attribute("WaterShorelineTintColor")?.Value);
                    variation = ParseColor(publicParams?.Attribute("WaterSurfaceColorVariation")?.Value);
                }
            }
        }
        catch
        {
            return null;
        }

        if (wet.R is null && ocean.R is null && shore.R is null && variation.R is null) return null;
        return new BodyPresentationMaterialHints(
            wet.R, wet.G, wet.B,
            ocean.R, ocean.G, ocean.B,
            shore.R, shore.G, shore.B,
            variation.R, variation.G, variation.B);
    }

    private static (double? R, double? G, double? B) ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return default;
        var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return default;
        return double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var r) &&
               double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var g) &&
               double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var b)
            ? (r, g, b)
            : default;
    }

    private static bool NeedsRebuild(string output, params string?[] inputs)
    {
        if (!File.Exists(output)) return true;
        var outputTime = File.GetLastWriteTimeUtc(output);
        foreach (var input in inputs)
        {
            if (!string.IsNullOrWhiteSpace(input) && File.Exists(input) && File.GetLastWriteTimeUtc(input) > outputTime)
                return true;
        }
        var algorithmPath = Path.Combine(Path.GetDirectoryName(output)!, ".algorithm-version");
        return !File.Exists(algorithmPath) || !string.Equals(File.ReadAllText(algorithmPath), DerivedAlgorithmVersion, StringComparison.Ordinal);
    }

    private static void EnsureOverviewTexture(string source, string destination)
    {
        try
        {
            if (!File.Exists(source)) return;
            if (File.Exists(destination) && File.GetLastWriteTimeUtc(destination) >= File.GetLastWriteTimeUtc(source))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            BitmapFrame frame;
            using (var stream = File.OpenRead(source))
            {
                var decoder = BitmapDecoder.Create(
                    stream,
                    BitmapCreateOptions.PreservePixelFormat,
                    BitmapCacheOption.OnLoad);
                frame = decoder.Frames[0];
            }

            const double maxDimension = 512.0;
            var scale = Math.Min(1.0, maxDimension / Math.Max(frame.PixelWidth, frame.PixelHeight));
            BitmapSource sourceBitmap = frame;
            if (scale < 0.999999)
            {
                sourceBitmap = new TransformedBitmap(
                    frame,
                    new System.Windows.Media.ScaleTransform(scale, scale));
            }

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(sourceBitmap));
            using var output = File.Create(destination);
            encoder.Save(output);
        }
        catch
        {
            // Overview textures are an optimization only. The full presentation texture remains valid.
        }
    }

    private static void CopyIfChanged(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(destination))
        {
            var sourceInfo = new FileInfo(source);
            var destinationInfo = new FileInfo(destination);
            if (sourceInfo.Length == destinationInfo.Length && sourceInfo.LastWriteTimeUtc <= destinationInfo.LastWriteTimeUtc)
                return;
        }
        File.Copy(source, destination, overwrite: true);
    }

    private static LiveSurfaceTextureCacheResult Empty(string status) =>
        new(null, new Dictionary<string, LiveSurfaceTextureAsset>(StringComparer.OrdinalIgnoreCase), status);

    private static (int? Width, int? Height) ReadPixelSize(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.FirstOrDefault();
            return frame is null ? (null, null) : (frame.PixelWidth, frame.PixelHeight);
        }
        catch
        {
            return (null, null);
        }
    }
}

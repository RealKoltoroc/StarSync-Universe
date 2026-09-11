using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using StarSyncUniverse.Data;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public sealed record BodyTextureArchiveResult(
    string RootPath,
    int BodyCount,
    int OriginalFileCount,
    int UpdatedFileCount,
    string DataStatus);

/// <summary>
/// Mirrors body presentation sources into a human-inspectable StarBreaker-side archive.
/// The archive deliberately separates immutable/original Data.p4k assets from StarSync's
/// geometry-preserving presentation output. Original files are never rotated, flipped,
/// cropped or rewritten. Updated files must keep the exact same spherical UV geometry.
/// </summary>
public static class BodyTextureArchiveService
{
    private sealed record TextureSource(string Key, string FilterSuffix, string DecodedCacheName, string[] Stems);

    private static readonly TextureSource[] Sources =
    [
        new("starmap", "*_starmap_diff.dds", "starmap-diff-textures-v2", ["{0}_global_starmap_diff", "{0}_starmap_diff"]),
        new("climate", "*_clim.dds", "terrain-climate-controls-v2", ["{0}_global_clim", "{0}_clim"]),
        new("splat", "*_splat.dds", "terrain-splat-controls-v2", ["{0}_global_splat", "{0}_splat"]),
        new("elevation", "*_elev.dds", "terrain-elevation-controls-v4", ["{0}_global_elev", "{0}_elev"]),
        new("normal", "*_ddn.dds", "body-normal-ddn-v4", ["{0}_global_ddn", "{0}_ddn"]),
        new("displacement", "*_displ.dds", "body-displacement-v1", ["{0}_global_displ", "{0}_displ"]),
        new("cloud", "*_cloud_global.dds", "body-cloud-global-v3", ["{0}_cloud_global", "{0}_global_cloud"]),
        new("clouds", "*_clouds_global.dds", "body-clouds-global-v3", ["{0}_clouds_global", "{0}_global_clouds"]),
        new("cloud-types", "*_cloud_types.dds", "body-cloud-types-v1", ["{0}_cloud_types", "{0}_global_cloud_types"]),
        new("cloud-tint", "*_cloud_tint_gradient.dds", "gas-giant-tint-gradients-v2", ["{0}_cloud_tint_gradient", "{0}_global_cloud_tint_gradient"])
    ];

    public static async Task<BodyTextureArchiveResult> ExportAsync(
        string system,
        IReadOnlyList<CelestialBodyPhysical> bodies,
        IReadOnlyList<UniverseEntity> entities,
        StarBreakerClient starBreaker,
        LiveSurfaceTextureCacheResult presentationCache,
        CancellationToken cancellationToken = default)
    {
        system = system.Trim().ToLowerInvariant();
        var archiveRoot = Path.Combine(ResolveStarBreakerHome(starBreaker.Executable), "BodyTextures");
        var systemRoot = Path.Combine(archiveRoot, system.ToUpperInvariant());
        if (Directory.Exists(systemRoot)) Directory.Delete(systemRoot, recursive: true);
        Directory.CreateDirectory(systemRoot);

        if (!starBreaker.IsAvailable)
            return new BodyTextureArchiveResult(systemRoot, 0, 0, 0, "STARBREAKER_UNAVAILABLE");

        var decodedRoots = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var rawRoots = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in Sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filter = $"Data/Textures/planets/global/{system}/**/{source.FilterSuffix}";
            decodedRoots[source.Key] = await TryExtractAsync(
                starBreaker, filter, $"{system}-{source.DecodedCacheName}", "dds-png", cancellationToken);
            var rawFilter = filter.EndsWith(".dds", StringComparison.OrdinalIgnoreCase) ? filter + "*" : filter;
            rawRoots[source.Key] = await TryExtractAsync(
                starBreaker, rawFilter, $"{system}-body-texture-archive-raw-{source.Key}-v2", null, cancellationToken);
        }

        var indexLines = new List<string>
        {
            "BodyName\tContainer\tPresentationKind\tOriginalFiles\tUpdatedFiles\tProjection\tOrientationStatus"
        };
        var originalCount = 0;
        var updatedCount = 0;

        foreach (var body in bodies.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(body.ContainerName)) continue;

            var container = body.ContainerName.Trim().ToLowerInvariant();
            var displayName = ResolveDisplayName(body, entities);
            var folderName = displayName.Equals(body.Name, StringComparison.OrdinalIgnoreCase) || displayName.Equals(container, StringComparison.OrdinalIgnoreCase)
                ? SanitizePathSegment(displayName)
                : $"{SanitizePathSegment(displayName)}__{SanitizePathSegment(container)}";
            var bodyFolder = Path.Combine(systemRoot, folderName);
            var originalFolder = Path.Combine(bodyFolder, "original");
            var updatedFolder = Path.Combine(bodyFolder, "updated");
            Directory.CreateDirectory(originalFolder);
            Directory.CreateDirectory(updatedFolder);

            // Keep each Data.p4k source exactly as extracted and also provide StarBreaker's decoded
            // PNG beside it for visual inspection. No image transformation occurs in this branch.
            foreach (var source in Sources)
            {
                foreach (var stemTemplate in source.Stems)
                {
                    var stem = string.Format(stemTemplate, container);
                    originalCount += CopySourceFamily(rawRoots[source.Key], system, container, stem + ".dds", originalFolder);
                    originalCount += CopySourceFile(decodedRoots[source.Key], system, container, stem + ".png", originalFolder);
                }
            }

            presentationCache.AssetsByBodyName.TryGetValue(body.Name, out var asset);
            if (asset is not null)
            {
                updatedCount += CopyNamed(asset.AssetPath, Path.Combine(updatedFolder, "surface-updated.png"));
                if (!string.IsNullOrWhiteSpace(asset.CloudAssetPath))
                    updatedCount += CopyNamed(asset.CloudAssetPath!, Path.Combine(updatedFolder, "cloud-updated.png"));
                if (!string.IsNullOrWhiteSpace(asset.NormalAssetPath))
                    updatedCount += CopyNamed(asset.NormalAssetPath!, Path.Combine(updatedFolder, "normal-updated.png"));

                var calibratedRecipe = container switch
                {
                    "pyro2" => new { SplatRole = "MATERIAL_MASK", DisplacementRole = "RELIEF_TONE", SplatMaxDimension = 768, OutputSize = 4096, Grade = "MONOX_BLUE_LAVENDER_ROSE_BODY_PALETTE" },
                    "pyro5e" => new { SplatRole = "MATERIAL_MASK", DisplacementRole = "RELIEF_TONE", SplatMaxDimension = 512, OutputSize = 4096, Grade = "FUEGO_RUST_ORANGE_OCHRE_BODY_PALETTE" },
                    "pyro6" => new { SplatRole = "MATERIAL_MASK", DisplacementRole = "RELIEF_TONE", SplatMaxDimension = 256, OutputSize = 4096, Grade = "TERMINUS_CHARCOAL_RUST_BLUEGRAY_CREAM_BODY_PALETTE" },
                    _ => null
                };
                if (calibratedRecipe is not null)
                {
                    await File.WriteAllTextAsync(
                        Path.Combine(updatedFolder, "processing.json"),
                        JsonSerializer.Serialize(new
                        {
                            Algorithm = "SPLAT_MASK_DISPLACEMENT_RELIEF_V16_BODY_PALETTE_MATERIAL_HINTS",
                            GeometryPreserved = true,
                            CloudsExcludedFromSurface = true,
                            Recipe = calibratedRecipe
                        }, new JsonSerializerOptions { WriteIndented = true }),
                        cancellationToken);
                    updatedCount++;
                }
            }

            var orientation = new
            {
                SchemaVersion = 1,
                System = system,
                DisplayName = displayName,
                PhysicalBodyName = body.Name,
                BodyContainer = body.ContainerName,
                BodySourceUuid = body.SourceUuid,
                RadiusMeters = body.RadiusMeters,
                Projection = "EQUIRECTANGULAR_SPHERE_UV",
                OriginalGeometry = new
                {
                    RotationDegrees = 0,
                    FlipX = false,
                    FlipY = false,
                    Crop = false,
                    Resample = false,
                    PreservedOneToOneFromDataP4k = true
                },
                UpdatedGeometry = new
                {
                    RotationDegrees = 0,
                    FlipX = false,
                    FlipY = false,
                    Crop = false,
                    GeometryPreserved = true,
                    QualityAndColorProcessingAllowed = true
                },
                BodyLocalToUv = new
                {
                    U = "fract(0.5 + atan2(Y,X)/(2*PI))",
                    V = "clamp(0.5 - asin(Z/R)/PI, 0, 1)",
                    R = "sqrt(X*X + Y*Y + Z*Z)"
                },
                RendererContract = "Surface targets and texture sampling use the same body-local XYZ sphere mapping before camera yaw/pitch projection.",
                OrientationStatus = "BODY_LOCAL_AXIS_MAPPING_SHARED_WITH_RENDERER__GEODETIC_ZERO_MERIDIAN_UNPROVEN",
                PresentationKind = asset?.AssetKind ?? "NONE",
                PresentationSource = asset?.SourceDdsPath,
                PresentationDataStatus = asset?.DataStatus
            };
            await File.WriteAllTextAsync(
                Path.Combine(bodyFolder, "orientation.json"),
                JsonSerializer.Serialize(orientation, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                }),
                cancellationToken);

            var originalFiles = Directory.EnumerateFiles(originalFolder).Select(Path.GetFileName).OrderBy(x => x).ToArray();
            var checksumLines = Directory.EnumerateFiles(originalFolder)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(path => $"{Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()}  {Path.GetFileName(path)}")
                .ToArray();
            await File.WriteAllLinesAsync(Path.Combine(bodyFolder, "original.sha256"), checksumLines, cancellationToken);

            var updatedFiles = Directory.EnumerateFiles(updatedFolder).Select(Path.GetFileName).OrderBy(x => x).ToArray();
            indexLines.Add(string.Join('\t',
                displayName,
                body.ContainerName,
                asset?.AssetKind ?? "NONE",
                string.Join(';', originalFiles),
                string.Join(';', updatedFiles),
                "EQUIRECTANGULAR_SPHERE_UV",
                "BODY_LOCAL_AXIS_MAPPING_SHARED_WITH_RENDERER"));
        }

        await File.WriteAllLinesAsync(Path.Combine(systemRoot, "texture-index.tsv"), indexLines, cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(systemRoot, "README.txt"),
            "StarSyncUniverse body texture inspection archive\r\n\r\n" +
            "original/: exact Data.p4k DDS files plus StarBreaker-decoded PNG previews. These files are never rotated, flipped, cropped or enhanced.\r\n" +
            "updated/: StarSync presentation outputs. Their spherical UV geometry is preserved so BodyFixed locations remain on the same UV coordinates.\r\n" +
            "orientation.json: per-body mapping contract and source metadata.\r\n\r\n" +
            "Important: some Pyro bodies do not ship a final body-specific starmap diffuse. In those cases original/ contains the available climate/splat/elevation/normal/cloud source layers separately; surface-updated.png is a derived presentation and is explicitly not an authoritative albedo.\r\n",
            cancellationToken);

        return new BodyTextureArchiveResult(
            systemRoot,
            bodies.Count,
            originalCount,
            updatedCount,
            "STARBREAKER_BODY_TEXTURE_ARCHIVE_READY");
    }

    private static async Task<string?> TryExtractAsync(
        StarBreakerClient starBreaker,
        string filter,
        string cacheName,
        string? convert,
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

    private static int CopySourceFamily(string? extractionRoot, string system, string container, string baseFileName, string destinationFolder)
    {
        if (string.IsNullOrWhiteSpace(extractionRoot)) return 0;
        var folder = Path.Combine(extractionRoot, "Data", "Textures", "planets", "global", system, container);
        if (!Directory.Exists(folder)) return 0;
        var files = Directory.EnumerateFiles(folder, baseFileName + "*", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path).Equals(baseFileName, StringComparison.OrdinalIgnoreCase) ||
                           Path.GetFileName(path).StartsWith(baseFileName + ".", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var copied = 0;
        foreach (var source in files)
            copied += CopyNamed(source, Path.Combine(destinationFolder, Path.GetFileName(source)));
        return copied;
    }

    private static int CopySourceFile(string? extractionRoot, string system, string container, string fileName, string destinationFolder)
    {
        if (string.IsNullOrWhiteSpace(extractionRoot)) return 0;
        var source = Path.Combine(extractionRoot, "Data", "Textures", "planets", "global", system, container, fileName);
        if (!File.Exists(source)) return 0;
        return CopyNamed(source, Path.Combine(destinationFolder, fileName));
    }

    private static int CopyNamed(string source, string destination)
    {
        if (!File.Exists(source)) return 0;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination, overwrite: true);
        return 1;
    }

    private static string ResolveStarBreakerHome(string executable)
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(executable))!);
        if (directory.Name.Equals("release", StringComparison.OrdinalIgnoreCase) &&
            directory.Parent?.Name.Equals("target", StringComparison.OrdinalIgnoreCase) == true &&
            directory.Parent.Parent is not null)
            return directory.Parent.Parent.FullName;
        return directory.FullName;
    }

    private static string ResolveDisplayName(CelestialBodyPhysical body, IReadOnlyList<UniverseEntity> entities)
    {
        if (!string.IsNullOrWhiteSpace(body.SourceUuid))
        {
            var byUuid = entities.FirstOrDefault(e =>
                !string.IsNullOrWhiteSpace(e.SourceUuid) &&
                e.SourceUuid.Equals(body.SourceUuid, StringComparison.OrdinalIgnoreCase) &&
                (e.Type.Equals("Planet", StringComparison.OrdinalIgnoreCase) || e.Type.Equals("Moon", StringComparison.OrdinalIgnoreCase)));
            if (byUuid is not null && !string.IsNullOrWhiteSpace(byUuid.Name)) return byUuid.Name;
        }

        var suffix = "/" + body.ContainerName.Trim().Replace('\\', '/').ToLowerInvariant() + ".socpak";
        var byPath = entities.FirstOrDefault(e =>
            !string.IsNullOrWhiteSpace(e.SourcePath) &&
            e.SourcePath.Replace('\\', '/').ToLowerInvariant().EndsWith(suffix, StringComparison.Ordinal) &&
            (e.Type.Equals("Planet", StringComparison.OrdinalIgnoreCase) || e.Type.Equals("Moon", StringComparison.OrdinalIgnoreCase)));
        return byPath is not null && !string.IsNullOrWhiteSpace(byPath.Name) ? byPath.Name : body.Name;
    }

    private static string SanitizePathSegment(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return string.IsNullOrWhiteSpace(value) ? "unnamed-body" : value.Trim();
    }
}

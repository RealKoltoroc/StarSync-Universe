using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class SurfaceTextureAssetResolver
{
    private static readonly string[] SupportedExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    public static SurfaceTextureDescriptor Resolve(
        string assetRoot,
        string bodyCanonicalId,
        string bodyName,
        string coordinateConvention)
    {
        var safeCanonicalId = Safe(bodyCanonicalId);
        var overrideRoot = Path.Combine(assetRoot, "Surfaces", "Overrides", "by-canonical-id");

        foreach (var extension in SupportedExtensions)
        {
            var candidate = Path.Combine(overrideRoot, safeCanonicalId + extension);
            if (!File.Exists(candidate))
                continue;

            var (pixelWidth, pixelHeight) = ReadPixelSize(candidate);
            return new SurfaceTextureDescriptor(
                bodyCanonicalId,
                bodyName,
                "EQUIRECTANGULAR",
                candidate,
                true,
                pixelWidth,
                pixelHeight,
                coordinateConvention,
                "LOCAL_OPTIONAL_PRESENTATION_ASSET",
                "PRESENT_UNREGISTERED_UNTIL_AXIS_CALIBRATION");
        }

        return new SurfaceTextureDescriptor(
            bodyCanonicalId,
            bodyName,
            "EQUIRECTANGULAR",
            Path.Combine(overrideRoot, safeCanonicalId + ".png"),
            false,
            null,
            null,
            coordinateConvention,
            "OPTIONAL_PRESENTATION_ASSET",
            "MISSING_OPTIONAL");
    }

    private static (int? Width, int? Height) ReadPixelSize(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
                stream,
                System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.FirstOrDefault();
            return frame is null ? (null, null) : (frame.PixelWidth, frame.PixelHeight);
        }
        catch
        {
            return (null, null);
        }
    }

    public static string Safe(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        return string.Concat(value.Select(c => invalid.Contains(c) || c == ':' ? '_' : c));
    }
}

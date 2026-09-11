using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StarSyncUniverse.Services;

public sealed record BodyPresentationMaterialHints(
    double? WetEdgeR = null,
    double? WetEdgeG = null,
    double? WetEdgeB = null,
    double? OceanR = null,
    double? OceanG = null,
    double? OceanB = null,
    double? ShoreR = null,
    double? ShoreG = null,
    double? ShoreB = null,
    double? SurfaceVariationR = null,
    double? SurfaceVariationG = null,
    double? SurfaceVariationB = null);

/// <summary>
/// Builds a restrained presentation-only body texture from CURRENT LIVE technical terrain maps.
/// Climate/splat maps are control data and are never exposed directly as RGB surface color.
/// The builder converts them to structure/tone and applies a compact body-local color-chart grade
/// when a CURRENT LIVE 16x16x16 CCH is available. Geometry and BodyFixed data are never modified.
/// </summary>
public static class BodyPresentationTextureBuilder
{
    private readonly record struct Rgb(double R, double G, double B)
    {
        public static Rgb Lerp(Rgb a, Rgb b, double t) => new(
            a.R + (b.R - a.R) * t,
            a.G + (b.G - a.G) * t,
            a.B + (b.B - a.B) * t);
    }

    private sealed record SurfaceReferenceProfile(
        Rgb Dark,
        Rgb Mid,
        Rgb Light,
        Rgb Warm,
        Rgb Cool,
        double ToneGamma,
        double ToneContrast = 1.0,
        double SplatWeight = 0.58,
        double DisplacementWeight = 0.34,
        double ClimateWeight = 0.06,
        double ElevationWeight = 0.02,
        int OutputSize = 2048);

    private static SurfaceReferenceProfile? ReferenceProfile(string? bodyKey)
    {
        var reference = PyroReferenceColorProfiles.Find(bodyKey);
        if (reference is null || reference.GasGiant) return null;
        static Rgb C(PresentationRgb value) => new(value.R, value.G, value.B);
        return new SurfaceReferenceProfile(
            C(reference.Dark),
            C(reference.Mid),
            C(reference.Light),
            C(reference.Warm),
            C(reference.Cool),
            1.0,
            ToneContrast: 1.0,
            SplatWeight: 0.66,
            DisplacementWeight: 0.32,
            ClimateWeight: 0.01,
            ElevationWeight: 0.01,
            OutputSize: reference.OutputSize);
    }

    public static bool TryBuild(
        string climatePng,
        string splatPng,
        string? elevationPng,
        string? displacementPng,
        BodyPresentationMaterialHints? materialHints,
        string? colorChartPng,
        string? bodyKey,
        string outputPng)
    {
        try
        {
            var climate = LoadBgra32(climatePng);
            var splat = LoadBgra32(splatPng);
            var elevation = !string.IsNullOrWhiteSpace(elevationPng) && File.Exists(elevationPng)
                ? LoadBgra32(elevationPng)
                : null;
            var displacement = !string.IsNullOrWhiteSpace(displacementPng) && File.Exists(displacementPng)
                ? LoadBgra32(displacementPng)
                : null;
            var chart = !string.IsNullOrWhiteSpace(colorChartPng) && File.Exists(colorChartPng)
                ? LoadBgra32(colorChartPng)
                : null;

            var profile = ReferenceProfile(bodyKey);
            var calibratedBody = bodyKey?.Trim().ToLowerInvariant();
            // Splat is a grayscale material/control field, not luminance/albedo. Preserve its regional layout,
            // but do not let high-valued splat texels become bright surface spots. Terminus is intentionally
            // smoothed more aggressively because its source splat contains compact high-value islands that
            // otherwise read as fake clouds when misinterpreted as brightness.
            var climatePresentation = PreparePresentationControl(climate, profile is null ? 128 : 256);
            var splatMaxDimension = calibratedBody switch
            {
                "pyro6" => 256,
                "pyro5e" => 512,
                "pyro2" => 768,
                "pyro1" or "pyro3" or "pyro4" or "pyro5a" or "pyro5b" or "pyro5c" or "pyro5d" or "pyro5f" => 768,
                _ => profile is null ? 160 : 768
            };
            var splatPresentation = PreparePresentationControl(splat, splatMaxDimension);
            var displacementPresentation = displacement is null ? null : PreparePresentationControl(displacement, profile is null ? 512 : 1024);
            var sourceWidth = Math.Max(climate.Width, Math.Max(splat.Width, Math.Max(elevation?.Width ?? 0, displacement?.Width ?? 0)));
            var sourceHeight = Math.Max(climate.Height, Math.Max(splat.Height, Math.Max(elevation?.Height ?? 0, displacement?.Height ?? 0)));
            // Geometry-preserving upsample. The calibrated Pyro examples are emitted at 4096² for inspection
            // and close-range presentation; UV topology is unchanged, so BodyFixed locations stay aligned.
            var targetSize = profile?.OutputSize ?? 2048;
            var width = Math.Clamp(Math.Max(targetSize, sourceWidth * 2), 1, targetSize);
            var height = Math.Clamp(Math.Max(targetSize, sourceHeight * 2), 1, targetSize);
            var output = new byte[width * height * 4];

            var wetHint = MaterialColor(materialHints?.WetEdgeR, materialHints?.WetEdgeG, materialHints?.WetEdgeB);
            var shoreHint = MaterialColor(materialHints?.ShoreR, materialHints?.ShoreG, materialHints?.ShoreB);
            var surfaceHint = MaterialColor(materialHints?.SurfaceVariationR, materialHints?.SurfaceVariationG, materialHints?.SurfaceVariationB);
            var oceanHint = MaterialColor(materialHints?.OceanR, materialHints?.OceanG, materialHints?.OceanB);
            // For bodies with a starcitizen.tools orbital reference, the measured presentation palette is the
            // final color authority. CURRENT LIVE material/CCH data still informs uncalibrated fallback bodies,
            // but it must not re-grade a reference-calibrated Pyro body away from its observed in-game colors.
            Rgb dark;
            Rgb mid;
            Rgb light;
            Rgb warm;
            Rgb cool;
            if (profile is not null)
            {
                dark = profile.Dark;
                mid = profile.Mid;
                light = profile.Light;
                warm = profile.Warm;
                cool = profile.Cool;
            }
            else
            {
                var defaultDark = new Rgb(0.115, 0.110, 0.105);
                var defaultMid = new Rgb(0.390, 0.365, 0.325);
                var defaultWarm = new Rgb(0.790, 0.445, 0.145);
                var defaultCool = new Rgb(0.310, 0.390, 0.420);
                var darkSeed = wetHint is { } wet ? Rgb.Lerp(defaultDark, ScaleColor(wet, 3.4), 0.72) : defaultDark;
                var midSeed = surfaceHint is { } surface ? Rgb.Lerp(defaultMid, surface, 0.38) : defaultMid;
                var warmSeed = shoreHint is { } shore && ColorWarmSignal(shoreHint) > 0.02 ? Rgb.Lerp(defaultWarm, shore, 0.64) : defaultWarm;
                var coolSeed = oceanHint is { } ocean && ColorCoolSignal(oceanHint) > 0.02 ? Rgb.Lerp(defaultCool, ocean, 0.42) : defaultCool;
                dark = wetHint is { } wetDirect ? Rgb.Lerp(Grade(darkSeed, chart), ScaleColor(wetDirect, 2.8), 0.34) : Grade(darkSeed, chart);
                mid = surfaceHint is { } surfaceDirect ? Rgb.Lerp(Grade(midSeed, chart), surfaceDirect, 0.32) : Grade(midSeed, chart);
                var lightSeed = Rgb.Lerp(new Rgb(0.735, 0.700, 0.620), midSeed, 0.22);
                light = Grade(lightSeed, chart);
                warm = shoreHint is { } shoreDirect && ColorWarmSignal(shoreHint) > 0.02 ? Rgb.Lerp(Grade(warmSeed, chart), shoreDirect, 0.68) : Grade(warmSeed, chart);
                cool = oceanHint is { } oceanDirect && ColorCoolSignal(oceanHint) > 0.02 ? Rgb.Lerp(Grade(coolSeed, chart), oceanDirect, 0.58) : Grade(coolSeed, chart);
            }

            var elevationRange = elevation is null ? (Min: 0.0, Max: 1.0) : EstimateRange(elevation);
            var splatRange = EstimateRange(splatPresentation);
            var displacementRange = displacementPresentation is null ? (Min: 0.0, Max: 1.0) : EstimateRange(displacementPresentation);
            var warmMaterialSignal = Math.Max(ColorWarmSignal(shoreHint), ColorWarmSignal(oceanHint));
            var coolMaterialSignal = Math.Max(ColorCoolSignal(shoreHint), ColorCoolSignal(oceanHint));

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    // The global climate/splat maps are terrain-control fields, not final albedo. Sample the
                    // prefiltered presentation controls so large-scale material/climate structure survives
                    // while control-map speckle does not become fake photographic terrain detail.
                    var climateSample = SampleRgbBilinear(climatePresentation, x, y, width, height);
                    var splatSample = SampleRgbBilinear(splatPresentation, x, y, width, height);

                    var cb = climateSample.B;
                    var cg = climateSample.G;
                    var cr = climateSample.R;
                    var sb = splatSample.B;
                    var sg = splatSample.G;
                    var sr = splatSample.R;

                    // Surface-first composition. For calibrated Pyro rocky bodies the visible structure is
                    // intentionally driven by SPLAT first and DISPLACEMENT second. Elevation is no longer a
                    // pseudo-albedo source; it contributes only a tiny macro-luminance bias. Clouds are separate.
                    var climateTone = cr * 0.30 + cg * 0.50 + cb * 0.20;
                    var splatTone = sr * 0.34 + sg * 0.46 + sb * 0.20;
                    var terrainHeight = elevation is null
                        ? 0.5
                        : SampleElevation(elevation, x, y, width, height, elevationRange.Min, elevationRange.Max);
                    var displacementSample = displacementPresentation is null
                        ? new Rgb(0.5, 0.5, 0.5)
                        : SampleRgbBilinear(displacementPresentation, x, y, width, height);
                    var displacementTone = displacementSample.R * 0.58 + displacementSample.G * 0.30 + displacementSample.B * 0.12;

                    // Calibrated Pyro bodies: interpret SPLAT as a material-region mask and DISPLACEMENT as
                    // local relief/tonal detail. The old implementation treated absolute splat intensity as
                    // surface brightness; on Terminus that turned compact splat islands into white cloud-like
                    // dots even though the actual cloud layer was disabled. This branch removes that semantic
                    // error and lets every calibrated Pyro rocky body keep the palette measured from its orbital reference.
                    if (profile is not null)
                    {
                        var material = NormalizeRange(splatTone, splatRange.Min, splatRange.Max);
                        var relief = displacementPresentation is null ? 0.5 : NormalizeRange(displacementTone, displacementRange.Min, displacementRange.Max);
                        var calibratedColor = CalibratedMaterialColor(
                            calibratedBody,
                            material,
                            dark,
                            mid,
                            light,
                            warm,
                            cool);
                        var detailAmplitude = calibratedBody switch
                        {
                            "pyro1" or "pyro3" or "pyro5a" or "pyro5f" => 0.12,
                            "pyro2" or "pyro5e" => 0.16,
                            "pyro6" => 0.12,
                            _ => 0.18
                        };
                        var referenceLightLuma = light.R * 0.2126 + light.G * 0.7152 + light.B * 0.0722;
                        var maxLuma = Math.Clamp(referenceLightLuma + 0.045, 0.38, 0.97);
                        var shade = 1.0 + (relief - 0.5) * detailAmplitude * 2.0;
                        var calibratedOutputColor = ModulateAndCapLuminance(calibratedColor, shade, maxLuma);
                        var calibratedOutputIndex = (y * width + x) * 4;
                        output[calibratedOutputIndex] = ToByte(calibratedOutputColor.B);
                        output[calibratedOutputIndex + 1] = ToByte(calibratedOutputColor.G);
                        output[calibratedOutputIndex + 2] = ToByte(calibratedOutputColor.R);
                        output[calibratedOutputIndex + 3] = 255;
                        continue;
                    }

                    var splatWeight = profile?.SplatWeight ?? 0.18;
                    var displacementWeight = profile?.DisplacementWeight ?? 0.38;
                    var climateWeight = profile?.ClimateWeight ?? 0.10;
                    var elevationWeight = profile?.ElevationWeight ?? 0.34;
                    var totalWeight = Math.Max(0.0001, splatWeight + displacementWeight + climateWeight + elevationWeight);
                    var tone = Clamp01(
                        0.50 +
                        (splatTone - 0.50) * (splatWeight / totalWeight) * 1.28 +
                        (displacementTone - 0.50) * (displacementWeight / totalWeight) * 1.18 +
                        (climateTone - 0.50) * (climateWeight / totalWeight) * 0.55 +
                        (terrainHeight - 0.50) * (elevationWeight / totalWeight) * 0.40);
                    if (profile is not null)
                        tone = Clamp01(0.5 + (tone - 0.5) * profile.ToneContrast);
                    tone = Math.Pow(tone, profile?.ToneGamma ?? 0.96);

                    var baseColor = tone < 0.52
                        ? Rgb.Lerp(dark, mid, tone / 0.52)
                        : Rgb.Lerp(mid, light, (tone - 0.52) / 0.48);

                    var warmControl = Clamp01((sr - sb) * 0.72 + (cr - cb) * 0.28);
                    var coolControl = Clamp01((sb - sr) * 0.62 + (cb - cr) * 0.38);
                    var structure = Clamp01((Math.Abs(sr - sg) + Math.Abs(cr - cg)) * 0.42);

                    var warmWeight = warmControl * (0.20 + structure * 0.24) * (1.0 + warmMaterialSignal * 2.2);
                    var coolWeight = coolControl * (0.12 + structure * 0.14) * (1.0 + coolMaterialSignal * 1.2);
                    var color = Rgb.Lerp(baseColor, warm, Clamp01(warmWeight));
                    color = Rgb.Lerp(color, cool, Clamp01(coolWeight));
                    if (warmMaterialSignal > 0.02)
                    {
                        var materialPattern = Clamp01(0.10 + splatTone * 0.34 + structure * 0.56);
                        color = Rgb.Lerp(color, warm, Clamp01(warmMaterialSignal * materialPattern * 0.82));
                    }
                    if (coolMaterialSignal > 0.04)
                    {
                        var materialPattern = Clamp01(0.06 + climateTone * 0.22 + structure * 0.32);
                        color = Rgb.Lerp(color, cool, Clamp01(coolMaterialSignal * materialPattern * 0.36));
                    }

                    // Elevation must not dominate the calibrated Pyro albedo. For generic fallback bodies it
                    // can still supply restrained macro relief; pyro2/pyro5e/pyro6 use splat+displacement instead.
                    if (elevation is not null && profile is null)
                    {
                        var h0 = terrainHeight;
                        var hx = SampleElevation(elevation, Math.Min(width - 1, x + 2), y, width, height, elevationRange.Min, elevationRange.Max);
                        var hy = SampleElevation(elevation, x, Math.Min(height - 1, y + 2), width, height, elevationRange.Min, elevationRange.Max);
                        var slope = Math.Clamp((h0 - hx) * 0.72 + (h0 - hy) * 0.48, -0.10, 0.10);
                        var altitudeContrast = (h0 - 0.5) * 0.075;
                        color = new Rgb(
                            Clamp01(color.R + slope + altitudeContrast),
                            Clamp01(color.G + slope + altitudeContrast),
                            Clamp01(color.B + slope + altitudeContrast));
                    }

                    // Keep the fallback photographic/rocky rather than false-color, but retain enough
                    // chroma for body-specific material hints to remain visible.
                    var luma = color.R * 0.2126 + color.G * 0.7152 + color.B * 0.0722;
                    var gray = new Rgb(luma, luma, luma);
                    color = Rgb.Lerp(gray, color, 0.86);

                    var oi = (y * width + x) * 4;
                    output[oi] = ToByte(color.B);
                    output[oi + 1] = ToByte(color.G);
                    output[oi + 2] = ToByte(color.R);
                    output[oi + 3] = 255;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPng)!);
            var bitmap = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                output,
                width * 4);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(outputPng);
            encoder.Save(stream);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryBuildGasGiant(
        string cloudGlobalPng,
        string tintGradientPng,
        string? colorChartPng,
        string? bodyKey,
        string outputPng)
    {
        try
        {
            var cloud = LoadBgra32(cloudGlobalPng);
            var tint = LoadBgra32(tintGradientPng);
            var chart = !string.IsNullOrWhiteSpace(colorChartPng) && File.Exists(colorChartPng)
                ? LoadBgra32(colorChartPng)
                : null;
            if (tint.Width < 2 || tint.Height < 1) return false;
            var reference = PyroReferenceColorProfiles.Find(bodyKey);
            static Rgb ReferenceRgb(PresentationRgb value) => new(value.R, value.G, value.B);

            var width = cloud.Width;
            var height = cloud.Height;
            var output = new byte[width * height * 4];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var i = (y * width + x) * 4;
                    var b = cloud.Pixels[i] / 255.0;
                    var g = cloud.Pixels[i + 1] / 255.0;
                    var r = cloud.Pixels[i + 2] / 255.0;
                    var tone = Clamp01(r * 0.34 + g * 0.46 + b * 0.20);
                    var tx = Math.Clamp((int)Math.Round(tone * (tint.Width - 1)), 0, tint.Width - 1);
                    var ti = tx * 4;
                    var color = new Rgb(
                        tint.Pixels[ti + 2] / 255.0,
                        tint.Pixels[ti + 1] / 255.0,
                        tint.Pixels[ti] / 255.0);
                    color = Grade(color, chart);

                    // Preserve cloud-field contrast without exposing control channels as hue.
                    var contrast = (tone - 0.5) * 0.18;
                    color = new Rgb(
                        Clamp01(color.R + contrast),
                        Clamp01(color.G + contrast),
                        Clamp01(color.B + contrast));
                    if (reference is not null)
                    {
                        var target = tone < 0.5
                            ? Rgb.Lerp(ReferenceRgb(reference.Dark), ReferenceRgb(reference.Mid), tone / 0.5)
                            : Rgb.Lerp(ReferenceRgb(reference.Mid), ReferenceRgb(reference.Light), (tone - 0.5) / 0.5);
                        color = Rgb.Lerp(color, target, 0.78);
                    }

                    output[i] = ToByte(color.B);
                    output[i + 1] = ToByte(color.G);
                    output[i + 2] = ToByte(color.R);
                    output[i + 3] = 255;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(outputPng)!);
            var bitmap = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                output,
                width * 4);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream = File.Create(outputPng);
            encoder.Save(stream);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static double NormalizeRange(double value, double min, double max) =>
        Clamp01((value - min) / Math.Max(0.0001, max - min));

    private static Rgb CalibratedMaterialColor(
        string? bodyKey,
        double material,
        Rgb dark,
        Rgb mid,
        Rgb light,
        Rgb warm,
        Rgb cool) => bodyKey switch
    {
        "pyro1" => PaletteStops(material, dark, cool, mid, light, light, 0.18, 0.46, 0.75, 0.92),
        "pyro2" => PaletteStops(material, dark, cool, mid, warm, light, 0.20, 0.46, 0.73, 0.91),
        "pyro3" => PaletteStops(material, dark, cool, mid, light, light, 0.18, 0.46, 0.75, 0.92),
        "pyro4" => PaletteStops(material, dark, cool, mid, warm, light, 0.18, 0.44, 0.72, 0.90),
        "pyro5a" => PaletteStops(material, dark, mid, warm, light, warm, 0.18, 0.45, 0.73, 0.91),
        "pyro5b" => PaletteStops(material, dark, warm, mid, light, mid, 0.20, 0.46, 0.74, 0.91),
        "pyro5c" => PaletteStops(material, dark, cool, mid, warm, light, 0.18, 0.44, 0.72, 0.90),
        "pyro5d" => PaletteStops(material, dark, mid, warm, light, warm, 0.18, 0.45, 0.73, 0.91),
        "pyro5e" => PaletteStops(material, dark, mid, warm, light, warm, 0.18, 0.46, 0.74, 0.91),
        "pyro5f" => PaletteStops(material, dark, cool, mid, warm, light, 0.18, 0.44, 0.72, 0.90),
        "pyro6" => PaletteStops(material, dark, warm, mid, light, light, 0.20, 0.48, 0.76, 0.92),
        _ => PaletteStops(material, dark, mid, warm, cool, light, 0.20, 0.45, 0.72, 0.90)
    };

    private static Rgb PaletteStops(
        double t,
        Rgb c0,
        Rgb c1,
        Rgb c2,
        Rgb c3,
        Rgb c4,
        double p1,
        double p2,
        double p3,
        double p4)
    {
        t = Clamp01(t);
        if (t <= p1) return Rgb.Lerp(c0, c1, t / Math.Max(0.0001, p1));
        if (t <= p2) return Rgb.Lerp(c1, c2, (t - p1) / Math.Max(0.0001, p2 - p1));
        if (t <= p3) return Rgb.Lerp(c2, c3, (t - p2) / Math.Max(0.0001, p3 - p2));
        if (t <= p4) return Rgb.Lerp(c3, c4, (t - p3) / Math.Max(0.0001, p4 - p3));
        return c4;
    }

    private static Rgb ModulateAndCapLuminance(Rgb color, double factor, double maxLuma)
    {
        color = new Rgb(
            Clamp01(color.R * factor),
            Clamp01(color.G * factor),
            Clamp01(color.B * factor));
        var luma = color.R * 0.2126 + color.G * 0.7152 + color.B * 0.0722;
        if (luma <= maxLuma || luma <= 0.0001) return color;
        var scale = maxLuma / luma;
        return new Rgb(color.R * scale, color.G * scale, color.B * scale);
    }

    private static Rgb Grade(Rgb color, ImageData? chart)
    {
        if (chart is null || chart.Width != 256 || chart.Height != 16)
            return color;

        var r = Math.Clamp((int)Math.Round(color.R * 15), 0, 15);
        var g = Math.Clamp((int)Math.Round(color.G * 15), 0, 15);
        var b = Math.Clamp((int)Math.Round(color.B * 15), 0, 15);

        // CryEngine CCH: 16 blue slices laid horizontally, each slice is 16x16 (R x G).
        var x = b * 16 + r;
        var y = g;
        var i = (y * chart.Width + x) * 4;
        return new Rgb(
            chart.Pixels[i + 2] / 255.0,
            chart.Pixels[i + 1] / 255.0,
            chart.Pixels[i] / 255.0);
    }

    private static ImageData LoadBgra32(string path)
    {
        using var stream = File.OpenRead(path);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var source = decoder.Frames[0];
        BitmapSource converted = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        return new ImageData(converted.PixelWidth, converted.PixelHeight, pixels);
    }

    private static Rgb? MaterialColor(double? r, double? g, double? b)
    {
        if (r is null || g is null || b is null) return null;
        return new Rgb(Clamp01(r.Value), Clamp01(g.Value), Clamp01(b.Value));
    }

    private static Rgb ScaleColor(Rgb color, double scale) => new(
        Clamp01(color.R * scale),
        Clamp01(color.G * scale),
        Clamp01(color.B * scale));

    private static double ColorWarmSignal(Rgb? color) => color is { } c
        ? Math.Max(0.0, c.R - Math.Max(c.G, c.B))
        : 0.0;

    private static double ColorCoolSignal(Rgb? color) => color is { } c
        ? Math.Max(0.0, Math.Max(c.G, c.B) - c.R)
        : 0.0;

    private static (double Min, double Max) EstimateRange(ImageData image)
    {
        var min = 1.0;
        var max = 0.0;
        var stepX = Math.Max(1, image.Width / 256);
        var stepY = Math.Max(1, image.Height / 256);
        for (var y = 0; y < image.Height; y += stepY)
        for (var x = 0; x < image.Width; x += stepX)
        {
            var i = (y * image.Width + x) * 4;
            var value = image.Pixels[i + 2] / 255.0;
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }
        if (max - min < 0.01) return (0.0, 1.0);
        return (min, max);
    }

    private static int SampleIndex(ImageData image, int x, int y, int targetWidth, int targetHeight)
    {
        var sx = Math.Clamp((int)((long)x * image.Width / Math.Max(1, targetWidth)), 0, image.Width - 1);
        var sy = Math.Clamp((int)((long)y * image.Height / Math.Max(1, targetHeight)), 0, image.Height - 1);
        return (sy * image.Width + sx) * 4;
    }

    private static Rgb SampleRgbBilinear(ImageData image, int x, int y, int targetWidth, int targetHeight)
    {
        var fx = targetWidth <= 1 ? 0.0 : (double)x * (image.Width - 1) / (targetWidth - 1);
        var fy = targetHeight <= 1 ? 0.0 : (double)y * (image.Height - 1) / (targetHeight - 1);
        var x0 = Math.Clamp((int)Math.Floor(fx), 0, image.Width - 1);
        var y0 = Math.Clamp((int)Math.Floor(fy), 0, image.Height - 1);
        var x1 = Math.Min(image.Width - 1, x0 + 1);
        var y1 = Math.Min(image.Height - 1, y0 + 1);
        var tx = fx - x0;
        var ty = fy - y0;
        Rgb At(int sx, int sy)
        {
            var i = (sy * image.Width + sx) * 4;
            return new Rgb(image.Pixels[i + 2] / 255.0, image.Pixels[i + 1] / 255.0, image.Pixels[i] / 255.0);
        }
        return Rgb.Lerp(Rgb.Lerp(At(x0, y0), At(x1, y0), tx), Rgb.Lerp(At(x0, y1), At(x1, y1), tx), ty);
    }

    private static ImageData PreparePresentationControl(ImageData image, int maxDimension)
    {
        maxDimension = Math.Max(32, maxDimension);
        var scale = Math.Min(1.0, (double)maxDimension / Math.Max(image.Width, image.Height));
        if (scale >= 0.999) return image;
        var width = Math.Max(32, (int)Math.Round(image.Width * scale));
        var height = Math.Max(32, (int)Math.Round(image.Height * scale));
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var c = SampleRgbBilinear(image, x, y, width, height);
            var i = (y * width + x) * 4;
            pixels[i] = ToByte(c.B);
            pixels[i + 1] = ToByte(c.G);
            pixels[i + 2] = ToByte(c.R);
            pixels[i + 3] = 255;
        }
        return new ImageData(width, height, pixels);
    }

    private static Rgb SampleRgbSmoothed(ImageData image, int x, int y, int targetWidth, int targetHeight, int radius)
    {
        radius = Math.Max(1, radius);
        var c0 = SampleRgbBilinear(image, x, y, targetWidth, targetHeight);
        var c1 = SampleRgbBilinear(image, Math.Clamp(x - radius, 0, targetWidth - 1), y, targetWidth, targetHeight);
        var c2 = SampleRgbBilinear(image, Math.Clamp(x + radius, 0, targetWidth - 1), y, targetWidth, targetHeight);
        var c3 = SampleRgbBilinear(image, x, Math.Clamp(y - radius, 0, targetHeight - 1), targetWidth, targetHeight);
        var c4 = SampleRgbBilinear(image, x, Math.Clamp(y + radius, 0, targetHeight - 1), targetWidth, targetHeight);
        var c5 = SampleRgbBilinear(image, Math.Clamp(x - radius, 0, targetWidth - 1), Math.Clamp(y - radius, 0, targetHeight - 1), targetWidth, targetHeight);
        var c6 = SampleRgbBilinear(image, Math.Clamp(x + radius, 0, targetWidth - 1), Math.Clamp(y - radius, 0, targetHeight - 1), targetWidth, targetHeight);
        var c7 = SampleRgbBilinear(image, Math.Clamp(x - radius, 0, targetWidth - 1), Math.Clamp(y + radius, 0, targetHeight - 1), targetWidth, targetHeight);
        var c8 = SampleRgbBilinear(image, Math.Clamp(x + radius, 0, targetWidth - 1), Math.Clamp(y + radius, 0, targetHeight - 1), targetWidth, targetHeight);
        var w0 = 4.0;
        var w1 = 2.0;
        var w2 = 1.0;
        var total = w0 + 4 * w1 + 4 * w2;
        return new Rgb(
            (c0.R * w0 + (c1.R + c2.R + c3.R + c4.R) * w1 + (c5.R + c6.R + c7.R + c8.R) * w2) / total,
            (c0.G * w0 + (c1.G + c2.G + c3.G + c4.G) * w1 + (c5.G + c6.G + c7.G + c8.G) * w2) / total,
            (c0.B * w0 + (c1.B + c2.B + c3.B + c4.B) * w1 + (c5.B + c6.B + c7.B + c8.B) * w2) / total);
    }

    private static double SampleElevation(
        ImageData image,
        int x,
        int y,
        int targetWidth,
        int targetHeight,
        double min,
        double max)
    {
        var value = SampleRgbBilinear(image, x, y, targetWidth, targetHeight).R;
        return Clamp01((value - min) / Math.Max(0.0001, max - min));
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);
    private static byte ToByte(double value) => (byte)Math.Clamp((int)Math.Round(value * 255), 0, 255);

    private sealed record ImageData(int Width, int Height, byte[] Pixels);
}

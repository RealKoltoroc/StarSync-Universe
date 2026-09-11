using System.Text.Json;

namespace StarSyncUniverse.Services;

public sealed record MapDisplaySettings(
    bool ShowStars,
    bool ShowPlanets,
    bool ShowStations,
    bool ShowSystemStations,
    bool ShowMissionStations,
    bool ExternalFreightElevatorsOnly,
    bool SurfaceFreightElevatorsOnly,
    bool SurfaceVehicleServicesOnly,
    bool SurfaceGaragesOnly,
    bool SurfaceLandingPadsOnly,
    bool ShowRacingTracks,
    bool ShowJumpPoints,
    bool ShowOrbits,
    bool ShowAsteroids,
    bool ShowLabels,
    bool ShowRegions,
    bool ShowHidden,
    bool BackgroundStars,
    bool MajorOnly,
    bool Guides,
    bool WebGlBodies,
    bool OrbitPlane,
    bool SurveyLighting,
    bool SurveyNightFilter)
{
    public static MapDisplaySettings Default { get; } = new(
        ShowStars: true,
        ShowPlanets: true,
        ShowStations: true,
        ShowSystemStations: true,
        ShowMissionStations: false,
        ExternalFreightElevatorsOnly: false,
        SurfaceFreightElevatorsOnly: false,
        SurfaceVehicleServicesOnly: false,
        SurfaceGaragesOnly: false,
        SurfaceLandingPadsOnly: false,
        ShowRacingTracks: true,
        ShowJumpPoints: true,
        ShowOrbits: true,
        ShowAsteroids: true,
        ShowLabels: true,
        ShowRegions: false,
        ShowHidden: false,
        BackgroundStars: true,
        MajorOnly: true,
        Guides: true,
        WebGlBodies: true,
        OrbitPlane: true,
        SurveyLighting: false,
        SurveyNightFilter: true);
}

public sealed class MapDisplaySettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly SemaphoreSlim _gate = new(1, 1);

    public MapDisplaySettingsService()
    {
        RootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StarSyncUniverse");
        SettingsPath = Path.Combine(RootPath, "display-settings.json");
    }

    public string RootPath { get; }
    public string SettingsPath { get; }

    public MapDisplaySettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return MapDisplaySettings.Default;
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<MapDisplaySettings>(json, JsonOptions)
                           ?? MapDisplaySettings.Default;
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(nameof(MapDisplaySettings.SurveyNightFilter), out _) &&
                !document.RootElement.TryGetProperty("surveyNightFilter", out _))
            {
                settings = settings with { SurveyNightFilter = true };
            }
            return settings;
        }
        catch
        {
            return MapDisplaySettings.Default;
        }
    }

    public async Task SaveAsync(MapDisplaySettings settings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(RootPath);
            var tempPath = SettingsPath + ".tmp";
            await File.WriteAllTextAsync(
                tempPath,
                JsonSerializer.Serialize(settings, JsonOptions),
                cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, SettingsPath, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }
}

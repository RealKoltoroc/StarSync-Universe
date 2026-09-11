using System.Text.Json;

namespace StarSyncUniverse.Services;

public sealed record StarSyncUniverseSettings(
    int SchemaVersion,
    bool LocalP4kUpdateAdapterEnabled,
    bool ScUnpackedImportEnabled,
    bool OnlineLocationEnrichmentEnabled,
    bool OnlineLocationEnrichmentConsent,
    string? StarBreakerPath,
    string? DataP4kPath,
    string? ScUnpackedRoot,
    bool SyncHostEnabled = false,
    string? SyncHostBaseUrl = null,
    string SyncHostTransportMode = "auto",
    string? SyncHostPlayerHandle = null,
    string? SyncHostOrganization = null,
    string UiLanguage = "en")
{
    public static StarSyncUniverseSettings Default { get; } = new(
        SchemaVersion: 3,
        LocalP4kUpdateAdapterEnabled: false,
        ScUnpackedImportEnabled: false,
        OnlineLocationEnrichmentEnabled: false,
        OnlineLocationEnrichmentConsent: false,
        StarBreakerPath: null,
        DataP4kPath: null,
        ScUnpackedRoot: null);

    public bool OnlineLocationEnrichmentActive =>
        OnlineLocationEnrichmentEnabled && OnlineLocationEnrichmentConsent;
}

public sealed class StarSyncUniverseSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public StarSyncUniverseSettingsService()
    {
        RootPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StarSyncUniverse");
        SettingsPath = Path.Combine(RootPath, "settings.json");
    }

    public string RootPath { get; }
    public string SettingsPath { get; }

    public StarSyncUniverseSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return StarSyncUniverseSettings.Default;
            var loaded = JsonSerializer.Deserialize<StarSyncUniverseSettings>(File.ReadAllText(SettingsPath), JsonOptions);
            if (loaded is null) return StarSyncUniverseSettings.Default;

            // Consent is a hard gate. An enabled flag without consent never activates networking.
            if (loaded.OnlineLocationEnrichmentEnabled && !loaded.OnlineLocationEnrichmentConsent)
                loaded = loaded with { OnlineLocationEnrichmentEnabled = false };

            var normalizedLanguage = string.Equals(loaded.UiLanguage, "de", StringComparison.OrdinalIgnoreCase) ? "de" : "en";
            if (!string.Equals(loaded.UiLanguage, normalizedLanguage, StringComparison.Ordinal))
                loaded = loaded with { UiLanguage = normalizedLanguage };
            return loaded;
        }
        catch
        {
            return StarSyncUniverseSettings.Default;
        }
    }

    public async Task SaveAsync(StarSyncUniverseSettings settings, CancellationToken cancellationToken = default)
    {
        if (settings.OnlineLocationEnrichmentEnabled && !settings.OnlineLocationEnrichmentConsent)
            settings = settings with { OnlineLocationEnrichmentEnabled = false };

        Directory.CreateDirectory(RootPath);
        var tempPath = SettingsPath + ".tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(settings, JsonOptions), cancellationToken);
            File.Move(tempPath, SettingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); }
                catch { }
            }
        }
    }
}

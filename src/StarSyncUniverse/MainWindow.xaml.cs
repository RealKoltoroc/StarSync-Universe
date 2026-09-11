using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StarSyncUniverse.Data;
using StarSyncUniverse.Localization;
using StarSyncUniverse.Renderer;
using StarSyncUniverse.Services;

namespace StarSyncUniverse;

/// <summary>
/// Privileged WPF host for StarSyncUniverse.
///
/// Owns application lifecycle, data-source loading, settings, native galaxy search and the trusted
/// WebView2 bridge. The embedded renderer is presentation/UI only; file/network/process operations
/// remain in this host or dedicated services. Game-derived values are kept native and must not pass
/// through the UI localization layer.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly string AppVersion =
        Assembly.GetExecutingAssembly().GetName().Version is { } version
            ? $"v{version.Major}.{version.Minor}.{version.Build}"
            : "v?";

    private sealed record GalaxySearchResult(
        string System,
        string Name,
        string Category,
        string Type,
        string? PlacementId,
        string? SurfaceAnchorId,
        string? BodyName,
        string SearchKey)
    {
        public override string ToString() => $"{Name}    {System.ToUpperInvariant()} · {Category}";
    }

    private readonly List<GalaxySearchResult> _galaxySearchIndex = [];
    private GalaxySearchResult? _pendingGalaxySearchFocus;
    private bool _suppressGalaxySearchTextChange;
    private CancellationTokenSource? _loadCts;
    private IReadOnlyDictionary<string, StarSyncUniverse.Domain.UniverseDataset>? _datasets;
    private IReadOnlyList<StarSyncUniverse.Domain.JumpConnection> _jumps = [];
    private UniverseRoutePlanner? _routePlanner;
    private BookmarkService? _bookmarkService;
    private BookmarkGroupService? _bookmarkGroupService;
    private readonly UniverseSyncHostClientIdentityService _syncHostIdentityService = new();
    private readonly UniverseSyncHostClientService _syncHostClientService;
    private readonly SupportDiagnosticsService _supportDiagnosticsService = new();
    private StarBreakerClient? _starBreaker;
    private ScUnpackedKnowledgeDatabase? _knowledgeDatabase;
    private readonly LocationImageCacheService _locationImageCache = new();
    private readonly LocationUserOverrideService _locationUserOverrideService = new();
    private readonly StarSyncUniverseSettingsService _settingsService = new();
    private readonly MapDisplaySettingsService _displaySettingsService = new();
    private StarSyncUniverseSettings _settings = StarSyncUniverseSettings.Default;
    private MapDisplaySettings _displaySettings = MapDisplaySettings.Default;
    private readonly Dictionary<string, LiveSurfaceTextureCacheResult> _liveSurfaceTextureCaches = new(StringComparer.OrdinalIgnoreCase);
    private bool _suppressSystemSelection;
    private bool _webMessageWired;
    private long _renderGeneration;
    private StarSyncUniverse.Domain.NavigationTarget? _navigationStart;
    private StarSyncUniverse.Domain.NavigationTarget? _navigationDestination;

    public MainWindow()
    {
        InitializeComponent();
        _syncHostClientService = new UniverseSyncHostClientService(_syncHostIdentityService);
        _settings = ResolveAutoDetectedDataSources(_settingsService.Load());
        _displaySettings = _displaySettingsService.Load();
        ApplySettingsUiState();
        SetStatus(Ui("status.initializing"));
        Loaded += async (_, _) => await ReloadAsync();
        Closed += (_, _) =>
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = null;
            if (_webMessageWired && MapView.CoreWebView2 is not null)
            {
                MapView.CoreWebView2.WebMessageReceived -= MapView_WebMessageReceived;
                _webMessageWired = false;
            }
        };
    }

    private string Ui(string key) => UiLocalizationCatalog.T(_settings.UiLanguage, key);

    private void ApplySettingsUiState()
    {
        var language = UiLocalizationCatalog.NormalizeLanguage(_settings.UiLanguage);
        string T(string key) => UiLocalizationCatalog.T(language, key);

        StarmapNavButton.Content = T("nav.starmap");
        DatabaseNavButton.Content = T("nav.database");
        ToolsNavButton.Content = T("nav.tools");
        SettingsNavButton.Content = T("nav.settings");
        AboutNavButton.Content = T("nav.about");
        GalaxySearchPlaceholder.Text = T("top.search.placeholder");
        GalaxySearchBox.ToolTip = T("top.search.tooltip");
        SystemLabelText.Text = T("top.system");
        CurrentDataText.Text = T("top.currentData");
        LoadingText.Text = T("status.readingLive");

        RefreshButton.Content = "↻  " + (_settings.LocalP4kUpdateAdapterEnabled
            ? T("top.reloadLive")
            : T("top.reloadCache"));
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await ReloadAsync();

    private async void StarmapNav_Click(object sender, RoutedEventArgs e) =>
        await ExecuteUiScriptAsync("closeWorkspace();setMapMode('system')");

    private async void DatabaseNav_Click(object sender, RoutedEventArgs e) =>
        await ExecuteUiScriptAsync("openWorkspace('database')");

    private async void ToolsNav_Click(object sender, RoutedEventArgs e) =>
        await ExecuteUiScriptAsync("openWorkspace('tools')");

    private async void SettingsNav_Click(object sender, RoutedEventArgs e) =>
        await ExecuteUiScriptAsync("openWorkspace('settings')");

    private async void AboutNav_Click(object sender, RoutedEventArgs e) =>
        await ExecuteUiScriptAsync("openWorkspace('about')");

    private async Task ExecuteUiScriptAsync(string script)
    {
        if (MapView.CoreWebView2 is null) return;
        try { await MapView.ExecuteScriptAsync(script); }
        catch (InvalidOperationException) { }
    }

    private void RebuildGalaxySearchIndex()
    {
        _galaxySearchIndex.Clear();
        if (_datasets is null) return;

        foreach (var pair in _datasets.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var entry in UniverseCatalogBuilder.Build(pair.Value))
            {
                var knowledge = _knowledgeDatabase?.MatchLocation(
                    entry.SourceUuid,
                    entry.Name,
                    pair.Key,
                    entry.ParentSourceUuid,
                    entry.SourcePath);
                var readableName = knowledge?.Location.Name ?? entry.Name;
                string?[] semanticTerms = knowledge is null
                    ? []
                    :
                    [
                        knowledge.Location.Description,
                        knowledge.Location.ParentName,
                        knowledge.Location.JurisdictionName,
                        knowledge.Location.FactionName,
                        knowledge.Location.FactionDescription,
                        knowledge.Location.FactionType,
                        knowledge.Location.FactionClassification,
                        .. knowledge.Location.FactionRelations.Select(x => (string?)x.Name),
                        .. knowledge.Location.Services.Select(x => (string?)x.Name),
                        .. knowledge.Location.TradeProduces.Select(x => (string?)x.Name),
                        .. knowledge.Location.TradeConsumes.Select(x => (string?)x.Name),
                        .. knowledge.Location.CommoditiesSold.Select(x => (string?)x.Name),
                        .. knowledge.Location.CommoditiesBought.Select(x => (string?)x.Name)
                    ];
                string?[] searchTerms =
                [
                    pair.Key, readableName, entry.Name, entry.Category, entry.Type, entry.EntityClass,
                    entry.SourceUuid, entry.SourcePath,
                    .. semanticTerms
                ];
                var searchKey = string.Join(" ", searchTerms.Where(x => !string.IsNullOrWhiteSpace(x))).ToLowerInvariant();
                _galaxySearchIndex.Add(new GalaxySearchResult(
                    pair.Key, readableName, entry.Category, entry.Type,
                    entry.PlacementId, null, null, searchKey));
            }

            foreach (var target in SurfaceTargetCatalogBuilder.Build(pair.Value))
            {
                var knowledge = _knowledgeDatabase?.MatchLocation(target.SourceUuid, target.Name, pair.Key, null, target.SourcePath);
                var readableName = knowledge?.Location.Name ?? target.Name;
                string?[] semanticTerms = knowledge is null
                    ? []
                    :
                    [
                        knowledge.Location.Description,
                        knowledge.Location.ParentName,
                        knowledge.Location.JurisdictionName,
                        knowledge.Location.FactionName,
                        knowledge.Location.FactionDescription,
                        knowledge.Location.FactionType,
                        knowledge.Location.FactionClassification,
                        .. knowledge.Location.FactionRelations.Select(x => (string?)x.Name),
                        .. knowledge.Location.Services.Select(x => (string?)x.Name),
                        .. knowledge.Location.TradeProduces.Select(x => (string?)x.Name),
                        .. knowledge.Location.TradeConsumes.Select(x => (string?)x.Name),
                        .. knowledge.Location.CommoditiesSold.Select(x => (string?)x.Name),
                        .. knowledge.Location.CommoditiesBought.Select(x => (string?)x.Name)
                    ];
                string?[] searchTerms =
                [
                    pair.Key, readableName, target.Name, target.Category, target.AnchorScope, target.BodyName,
                    target.SourceUuid, target.EntityClass, target.SourcePath,
                    .. semanticTerms
                ];
                var searchKey = string.Join(" ", searchTerms.Where(x => !string.IsNullOrWhiteSpace(x))).ToLowerInvariant();
                _galaxySearchIndex.Add(new GalaxySearchResult(
                    pair.Key, readableName, target.Category, target.AnchorScope,
                    null, target.AnchorId, target.BodyName, searchKey));
            }
        }
    }

    private static int GalaxySearchRank(GalaxySearchResult result, string query)
    {
        var name = result.Name.ToLowerInvariant();
        if (name.Equals(query, StringComparison.Ordinal)) return 0;
        if (name.StartsWith(query, StringComparison.Ordinal)) return 1;
        if (result.System.Equals(query, StringComparison.OrdinalIgnoreCase)) return 2;
        return 3;
    }

    private void GalaxySearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = GalaxySearchBox.Text.Trim().ToLowerInvariant();
        GalaxySearchPlaceholder.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        GalaxySearchClearButton.Visibility = query.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (_suppressGalaxySearchTextChange || query.Length == 0 || _galaxySearchIndex.Count == 0)
        {
            GalaxySearchPopup.IsOpen = false;
            GalaxySearchResults.ItemsSource = null;
            return;
        }

        var matches = _galaxySearchIndex
            .Where(x => x.SearchKey.Contains(query, StringComparison.Ordinal))
            .OrderBy(x => GalaxySearchRank(x, query))
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.System, StringComparer.OrdinalIgnoreCase)
            .Take(14)
            .ToArray();
        GalaxySearchResults.ItemsSource = matches;
        GalaxySearchResults.SelectedIndex = matches.Length > 0 ? 0 : -1;
        GalaxySearchPopup.IsOpen = matches.Length > 0;
    }

    private async void GalaxySearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Down or Key.Up)
        {
            if (GalaxySearchResults.Items.Count == 0) return;
            e.Handled = true;
            var delta = e.Key == Key.Down ? 1 : -1;
            var current = GalaxySearchResults.SelectedIndex < 0 ? 0 : GalaxySearchResults.SelectedIndex;
            GalaxySearchResults.SelectedIndex = Math.Clamp(current + delta, 0, GalaxySearchResults.Items.Count - 1);
            GalaxySearchResults.ScrollIntoView(GalaxySearchResults.SelectedItem);
            GalaxySearchPopup.IsOpen = true;
            return;
        }
        if (e.Key == Key.Escape)
        {
            GalaxySearchPopup.IsOpen = false;
            return;
        }
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        if (GalaxySearchResults.SelectedItem is GalaxySearchResult selected)
            await NavigateToGalaxySearchResultAsync(selected);
    }

    private async void GalaxySearchResults_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (GalaxySearchResults.SelectedItem is GalaxySearchResult selected)
            await NavigateToGalaxySearchResultAsync(selected);
    }

    private void GalaxySearchClearButton_Click(object sender, RoutedEventArgs e)
    {
        GalaxySearchBox.Clear();
        GalaxySearchPopup.IsOpen = false;
        GalaxySearchBox.Focus();
    }

    private async Task NavigateToGalaxySearchResultAsync(GalaxySearchResult target)
    {
        GalaxySearchPopup.IsOpen = false;
        _suppressGalaxySearchTextChange = true;
        GalaxySearchBox.Text = target.Name;
        GalaxySearchBox.CaretIndex = GalaxySearchBox.Text.Length;
        _suppressGalaxySearchTextChange = false;
        GalaxySearchPlaceholder.Visibility = Visibility.Collapsed;
        GalaxySearchClearButton.Visibility = Visibility.Visible;

        if (SystemComboBox.SelectedItem is string current &&
            current.Equals(target.System, StringComparison.OrdinalIgnoreCase))
        {
            await FocusGalaxySearchResultInRendererAsync(target);
            return;
        }

        _pendingGalaxySearchFocus = target;
        var systemKey = _datasets?.Keys.FirstOrDefault(x => x.Equals(target.System, StringComparison.OrdinalIgnoreCase));
        if (systemKey is not null)
            SystemComboBox.SelectedItem = systemKey;
    }

    private async Task FocusGalaxySearchResultInRendererAsync(GalaxySearchResult target)
    {
        var placementId = JsonSerializer.Serialize(target.PlacementId);
        var surfaceAnchorId = JsonSerializer.Serialize(target.SurfaceAnchorId);
        await ExecuteUiScriptAsync($"closeWorkspace();focusSearchTargetByIdentity({placementId},{surfaceAnchorId});");
    }

    private async void SystemComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_suppressSystemSelection || _datasets is null || SystemComboBox.SelectedItem is not string system)
            return;
        await RenderDatasetAsync(system);
    }

    private async Task ReloadAsync()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;
        var sw = Stopwatch.StartNew();
        var bootScreenTimer = Stopwatch.StartNew();
        var minimumBootScreenDuration = TimeSpan.FromSeconds(4);

        _settings = ResolveAutoDetectedDataSources(_settingsService.Load());
        _displaySettings = _displaySettingsService.Load();
        ApplySettingsUiState();
        LoadingOverlay.Visibility = Visibility.Visible;
        RefreshButton.IsEnabled = false;

        try
        {
            ScUnpackedCatalog catalog = ScUnpackedCatalog.Empty;
            string? scRoot = null;

            // Readable location identity is a packaged part of the community build. The external
            // SCUnpacked adapter below is only a refresh/enrichment source; it is not required for
            // first start and disabling it must never revert known objects to raw Data.p4k labels.
            var bundledKnowledgePath = Path.Combine(
                AppContext.BaseDirectory,
                "Assets", "CommunityBaseline", "Knowledge", "location-knowledge.json");
            _knowledgeDatabase = File.Exists(bundledKnowledgePath)
                ? ScUnpackedKnowledgeDatabase.LoadBundledBaseline(bundledKnowledgePath)
                : null;

            if (_settings.ScUnpackedImportEnabled)
            {
                LoadingText.Text = Ui("status.locatingSc");
                if (!string.IsNullOrWhiteSpace(_settings.ScUnpackedRoot))
                {
                    scRoot = Path.GetFullPath(_settings.ScUnpackedRoot);
                }
                else
                {
                    throw new DirectoryNotFoundException(
                        "No SCUnpacked dataset was auto-detected below the application /database directory. " +
                        "Copy scunpacked-data there or configure its root path in Settings.");
                }

                var positionsPath = Path.Combine(scRoot, "starmap_positions.json");
                if (!File.Exists(positionsPath))
                    throw new FileNotFoundException("SCUnpacked starmap_positions.json not found.", positionsPath);

                catalog = ScUnpackedCatalog.Load(positionsPath);
                LoadingText.Text = Ui("status.refreshingKnowledge");
                var refreshedKnowledge = ScUnpackedKnowledgeDatabase.Load(scRoot);
                _knowledgeDatabase = _knowledgeDatabase is null
                    ? refreshedKnowledge
                    : ScUnpackedKnowledgeDatabase.Merge(_knowledgeDatabase, refreshedKnowledge);
            }

            IReadOnlyDictionary<string, StarSyncUniverse.Domain.UniverseDataset> datasets;
            _liveSurfaceTextureCaches.Clear();

            if (_settings.LocalP4kUpdateAdapterEnabled)
            {
                var starBreakerPath = _settings.StarBreakerPath
                    ?? Environment.GetEnvironmentVariable("STARSYNC_STARBREAKER")
                    ?? string.Empty;
                if (!File.Exists(starBreakerPath))
                    throw new FileNotFoundException(
                        "StarBreaker was not found. Copy starbreaker.exe into the StarSyncUniverse application directory " +
                        "(or a subdirectory) or configure its path in Settings.", starBreakerPath);
                var dataP4k = FirstNonEmpty(
                    _settings.DataP4kPath,
                    Environment.GetEnvironmentVariable("STARSYNC_DATA_P4K"));
                if (string.IsNullOrWhiteSpace(dataP4k))
                    throw new InvalidOperationException("Configure the Star Citizen Data.p4k path in Settings or STARSYNC_DATA_P4K before enabling the local update adapter.");
                var starBreaker = new StarBreakerClient(starBreakerPath, dataP4k);
                _starBreaker = starBreaker;

                var progress = new Progress<string>(message => LoadingText.Text = message);
                datasets = await new UniverseImportCoordinator(starBreaker, catalog).ImportAllAsync(progress, ct);

                // Build-specific station infrastructure is resolved after the spatial import because
                // it requires the final station placement/source path plus optional SCUnpacked identity.
                // Positive external-freight-elevator results are always backed by a CURRENT LIVE
                // ObjectContainer child reference; SCUnpacked is corroboration, never the authority.
                await ExternalFreightElevatorDetector.PopulateAsync(
                    datasets,
                    starBreaker,
                    _knowledgeDatabase,
                    progress,
                    ct);
                foreach (var pair in datasets)
                    await UniverseSnapshotWriter.WriteAsync(pair.Value, ct);

                foreach (var pair in datasets)
                {
                    ct.ThrowIfCancellationRequested();

                    // Presentation textures are a persistent visual layer, not a LIVE-mode feature.
                    // Load the last known-good presentation first so a failed/partial update can never
                    // blank already available bodies. LIVE mode may refresh this layer, but does not own it.
                    var existingPresentation = LiveSurfaceTextureCache.LoadExisting(pair.Key, pair.Value.Bodies);
                    LoadingText.Text = $"{Ui("status.refreshingTextures")} {pair.Key.ToUpperInvariant()}…";
                    var refreshedPresentation = await LiveSurfaceTextureCache.PrepareAsync(
                        pair.Key,
                        pair.Value.Bodies,
                        starBreaker,
                        ct);
                    _liveSurfaceTextureCaches[pair.Key] = LiveSurfaceTextureCache.PreferRefreshed(
                        existingPresentation,
                        refreshedPresentation,
                        pair.Value.Bodies.Count);

                    LoadingText.Text = $"{Ui("status.archivingTextures")} {pair.Key.ToUpperInvariant()}…";
                    _ = await BodyTextureArchiveService.ExportAsync(
                        pair.Key,
                        pair.Value.Bodies,
                        pair.Value.Entities,
                        starBreaker,
                        _liveSurfaceTextureCaches[pair.Key],
                        ct);
                }
            }
            else
            {
                _starBreaker = null;
                LoadingText.Text = Ui("status.cachedSnapshots");
                datasets = await UniverseSnapshotReader.LoadAllAsync(ct);
            }

            // Texture presentation is intentionally independent from the selected data mode.
            // Standalone, SCUnpacked-only, online-enriched and LIVE operation all use the same
            // persistent body-presentation layer. The P4K adapter only updates that layer when enabled.
            foreach (var pair in datasets)
            {
                ct.ThrowIfCancellationRequested();
                var existingPresentation = LiveSurfaceTextureCache.LoadExisting(pair.Key, pair.Value.Bodies);
                if (_liveSurfaceTextureCaches.TryGetValue(pair.Key, out var refreshedPresentation))
                {
                    _liveSurfaceTextureCaches[pair.Key] = LiveSurfaceTextureCache.PreferRefreshed(
                        existingPresentation,
                        refreshedPresentation,
                        pair.Value.Bodies.Count);
                }
                else
                {
                    _liveSurfaceTextureCaches[pair.Key] = existingPresentation;
                }
            }

            _datasets = datasets;
            if (_knowledgeDatabase is not null)
            {
                LoadingText.Text = _settings.LocalP4kUpdateAdapterEnabled
                    ? "Correlating local LIVE Data.p4k objects with SCUnpacked locations…"
                    : "Correlating cached universe objects with optional SCUnpacked locations…";
                _ = await _knowledgeDatabase.WriteAuditAsync(datasets, ct);
            }

            RebuildGalaxySearchIndex();
            _jumps = JumpGraphBuilder.Build(datasets);
            _routePlanner = new UniverseRoutePlanner(datasets, _jumps);
            var bookmarkStore = new LocalBookmarkStore(identityService: _syncHostIdentityService);
            _bookmarkService = new BookmarkService(new UniverseService(datasets, _jumps), bookmarkStore);
            _bookmarkGroupService = new BookmarkGroupService(bookmarkStore, _syncHostIdentityService);

            // Texture preparation can use large temporary decode buffers. Compact while the loading
            // overlay is still visible rather than retaining decoder working memory for WebView2.
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

            _suppressSystemSelection = true;
            var previousSystem = SystemComboBox.SelectedItem as string;
            SystemComboBox.Items.Clear();
            foreach (var system in new[] { "stanton", "pyro", "nyx" })
                if (datasets.ContainsKey(system)) SystemComboBox.Items.Add(system);
            SystemComboBox.SelectedItem = previousSystem is not null && datasets.ContainsKey(previousSystem)
                ? previousSystem
                : datasets.ContainsKey("stanton") ? "stanton" : datasets.Keys.FirstOrDefault();
            SystemComboBox.IsEnabled = SystemComboBox.Items.Count > 0;
            _suppressSystemSelection = false;

            if (SystemComboBox.SelectedItem is not string selectedSystem)
                throw new InvalidDataException("No renderable universe system is available.");

            LoadingText.Text = Ui("status.preloadingViewer");
            await RenderDatasetAsync(selectedSystem, waitForNavigation: true, ct);

            sw.Stop();
            var totals = datasets.Values.Sum(d => d.Entities.Count);
            var bodies = datasets.Values.Sum(d => d.Bodies.Count);
            var regions = datasets.Values.Sum(d => d.Regions.Count);
            var volumes = datasets.Values.Sum(d => d.Volumes.Count);
            var validationFailures = datasets.Values.Count(d => d.Diagnostics.Any(x => x.StartsWith("Validation: FAIL", StringComparison.Ordinal)));
            var sourceMode = _settings.LocalP4kUpdateAdapterEnabled ? "LOCAL LIVE Data.p4k adapter" : "CACHED SNAPSHOT";
            var semanticMode = _settings.ScUnpackedImportEnabled
                ? "packaged location baseline + SCUnpacked refresh"
                : "packaged location baseline";
            var onlineMode = _settings.OnlineLocationEnrichmentActive ? "online enrichment consented" : "online enrichment off";

            SetStatus(
                $"{sourceMode} · {semanticMode} · {onlineMode} · {totals:N0} spatial entities · {bodies:N0} bodies · {regions:N0} ring region(s) · {volumes:N0} volume region(s) · " +
                $"validation failures: {validationFailures} · loaded in {sw.Elapsed.TotalSeconds:N1}s");

            var remainingBootScreenTime = minimumBootScreenDuration - bootScreenTimer.Elapsed;
            if (remainingBootScreenTime > TimeSpan.Zero)
                await Task.Delay(remainingBootScreenTime, ct);

            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
            SetStatus("Load cancelled.");
        }
        catch (Exception ex)
        {
            LoadingText.Text = Ui("status.loadFailed") + "\n\n" + ex.Message +
                "\n\nOpen Settings to configure the Local Data.p4k adapter, optional SCUnpacked import, and online enrichment consent.";
            SetStatus("Load failed.");
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    private static StarSyncUniverseSettings ResolveAutoDetectedDataSources(StarSyncUniverseSettings settings)
    {
        string? starBreakerPath = settings.StarBreakerPath;
        if (string.IsNullOrWhiteSpace(starBreakerPath) || !File.Exists(starBreakerPath))
        {
            var appRoot = AppContext.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(appRoot, "starbreaker.exe"),
                Path.Combine(appRoot, "StarBreaker", "starbreaker.exe")
            };
            starBreakerPath = candidates.FirstOrDefault(File.Exists);
            if (starBreakerPath is null)
            {
                try
                {
                    starBreakerPath = Directory.EnumerateFiles(appRoot, "starbreaker.exe", SearchOption.AllDirectories)
                        .FirstOrDefault(path => !path.Contains(".WebView2", StringComparison.OrdinalIgnoreCase));
                }
                catch { }
            }
        }

        string? scUnpackedRoot = settings.ScUnpackedRoot;
        if (string.IsNullOrWhiteSpace(scUnpackedRoot) || !Directory.Exists(scUnpackedRoot) ||
            !File.Exists(Path.Combine(scUnpackedRoot, "starmap_positions.json")))
        {
            var databaseRoot = Path.Combine(AppContext.BaseDirectory, "database");
            if (Directory.Exists(databaseRoot))
            {
                if (File.Exists(Path.Combine(databaseRoot, "starmap_positions.json")))
                {
                    scUnpackedRoot = databaseRoot;
                }
                else
                {
                    try
                    {
                        var positions = Directory.EnumerateFiles(databaseRoot, "starmap_positions.json", SearchOption.AllDirectories)
                            .FirstOrDefault();
                        if (positions is not null)
                            scUnpackedRoot = Path.GetDirectoryName(positions);
                    }
                    catch { }
                }
            }
        }

        return settings with
        {
            StarBreakerPath = starBreakerPath,
            ScUnpackedRoot = scUnpackedRoot
        };
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.First(x => !string.IsNullOrWhiteSpace(x))!;

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task RenderDatasetAsync(string system, bool waitForNavigation = false, CancellationToken cancellationToken = default)
    {
        if (_datasets is null || !_datasets.TryGetValue(system, out var dataset))
            return;

        var renderGeneration = Interlocked.Increment(ref _renderGeneration);
        system = dataset.System;
        LiveSurfaceTextureCacheResult liveTextureCache;
        if (!_liveSurfaceTextureCaches.TryGetValue(system, out liveTextureCache!))
        {
            // The packaged/cached presentation layer is always the baseline, independent of data mode.
            liveTextureCache = LiveSurfaceTextureCache.LoadExisting(system, dataset.Bodies);
            if (_starBreaker is not null)
            {
                var refreshed = await LiveSurfaceTextureCache.PrepareAsync(
                    system,
                    dataset.Bodies,
                    _starBreaker,
                    _loadCts?.Token ?? CancellationToken.None);
                liveTextureCache = LiveSurfaceTextureCache.PreferRefreshed(
                    liveTextureCache,
                    refreshed,
                    dataset.Bodies.Count);
            }
            _liveSurfaceTextureCaches[system] = liveTextureCache;
        }

        if (renderGeneration != Volatile.Read(ref _renderGeneration) ||
            SystemComboBox.SelectedItem is not string selectedSystem ||
            !selectedSystem.Equals(system, StringComparison.OrdinalIgnoreCase))
            return;

        await MapView.EnsureCoreWebView2Async();
        if (renderGeneration != Volatile.Read(ref _renderGeneration))
            return;
        var mapVisualRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "MapVisuals");
        if (Directory.Exists(mapVisualRoot))
        {
            MapView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "starsync-assets",
                mapVisualRoot,
                Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
        }
        var brandingRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Branding");
        if (Directory.Exists(brandingRoot))
        {
            MapView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "starsync-branding",
                brandingRoot,
                Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
        }
        if (Directory.Exists(_locationImageCache.RootPath))
        {
            MapView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "starsync-location-images",
                _locationImageCache.RootPath,
                Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
        }
        Directory.CreateDirectory(_locationUserOverrideService.ImageRootPath);
        MapView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "starsync-location-overrides",
            _locationUserOverrideService.ImageRootPath,
            Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
        if (!string.IsNullOrWhiteSpace(liveTextureCache.RootPath) && Directory.Exists(liveTextureCache.RootPath))
        {
            MapView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "starsync-live-textures",
                liveTextureCache.RootPath,
                Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
        }
        else
        {
            MapView.CoreWebView2.ClearVirtualHostNameToFolderMapping("starsync-live-textures");
        }
        if (!_webMessageWired)
        {
            MapView.CoreWebView2.WebMessageReceived += MapView_WebMessageReceived;
            _webMessageWired = true;
        }
        var bookmarks = _bookmarkService is null ? [] : await _bookmarkService.ListAsync();
        var bookmarkGroups = _bookmarkGroupService is null ? [] : await _bookmarkGroupService.ListAsync();
        var locationOverrides = await _locationUserOverrideService.ListAsync(_loadCts?.Token ?? CancellationToken.None);
        var renderedHtml = MapHtmlRenderer.Build(
            dataset,
            _datasets,
            _navigationStart,
            _navigationDestination,
            bookmarks,
            bookmarkGroups,
            locationOverrides,
            liveTextureCache.AssetsByBodyName,
            _jumps,
            _knowledgeDatabase,
            _settings,
            _displaySettings);
        var rendererCacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StarSyncUniverse", "RendererCache");
        Directory.CreateDirectory(rendererCacheRoot);
        var rendererPath = Path.Combine(rendererCacheRoot, $"map-{dataset.System.ToLowerInvariant()}.html");
        await File.WriteAllTextAsync(rendererPath, renderedHtml, _loadCts?.Token ?? CancellationToken.None);

        TaskCompletionSource<bool>? navigationCompletion = null;
        void NavigationCompleted(object? _, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess) navigationCompletion?.TrySetResult(true);
            else navigationCompletion?.TrySetException(new InvalidOperationException($"WebView preload failed: {args.WebErrorStatus}"));
        }

        if (waitForNavigation)
        {
            navigationCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            MapView.CoreWebView2.NavigationCompleted += NavigationCompleted;
        }

        try
        {
            MapView.CoreWebView2.Navigate(new Uri(rendererPath).AbsoluteUri);
            if (navigationCompletion is not null)
                await navigationCompletion.Task.WaitAsync(cancellationToken);
        }
        finally
        {
            if (navigationCompletion is not null)
                MapView.CoreWebView2.NavigationCompleted -= NavigationCompleted;
        }

        SetStatus(
            $"{dataset.System.ToUpperInvariant()} · {dataset.Build} · {dataset.Entities.Count:N0} entities · {dataset.Bodies.Count:N0} bodies · " +
            $"{dataset.BodyAnchors.Count:N0} body-local anchors · {dataset.TemporalTransforms.Count:N0} temporal transforms · " +
            $"{liveTextureCache.AssetsByBodyName.Count:N0} presentation-safe CURRENT-LIVE body texture(s) · {liveTextureCache.DataStatus}");
    }

    private async Task PostBookmarkStateAsync(CancellationToken cancellationToken = default)
    {
        if (MapView.CoreWebView2 is null) return;
        var bookmarks = _bookmarkService is null ? [] : await _bookmarkService.ListAsync(cancellationToken);
        var groups = _bookmarkGroupService is null ? [] : await _bookmarkGroupService.ListAsync(cancellationToken);
        MapView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
        {
            type = "bookmarkState",
            bookmarks,
            bookmarkGroups = groups
        }));
    }

    private void PostLocationOverrideResult(string key, LocationUserOverrideRecord? record, string status)
    {
        if (MapView.CoreWebView2 is null) return;
        MapView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
        {
            type = "locationOverrideResult",
            key,
            record,
            status
        }));
    }

    private void MapView_WebMessageReceived(
        object? sender,
        Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs args)
    {
        // Host messages are privileged (settings, bookmarks, routes, diagnostics). Accept them only
        // from our generated local renderer cache, never from an arbitrary page loaded in WebView2.
        if (!Uri.TryCreate(args.Source, UriKind.Absolute, out var sourceUri) || !sourceUri.IsFile)
            return;

        var rendererRoot = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StarSyncUniverse", "RendererCache"));
        var sourcePath = Path.GetFullPath(sourceUri.LocalPath);
        var rootPrefix = rendererRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!sourcePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            return;

        HandleWebMessage(args.WebMessageAsJson);
    }

    private async void HandleWebMessage(string json)
    {
        if (_datasets is null || MapView.CoreWebView2 is null) return;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var type))
                return;

            var messageType = type.GetString();
            if (string.Equals(messageType, "rendererError", StringComparison.Ordinal))
            {
                var message = root.TryGetProperty("message", out var messageProperty) ? messageProperty.GetString() ?? "JavaScript renderer error" : "JavaScript renderer error";
                var line = root.TryGetProperty("line", out var lineProperty) && lineProperty.TryGetInt32(out var parsedLine) ? parsedLine : 0;
                var column = root.TryGetProperty("column", out var columnProperty) && columnProperty.TryGetInt32(out var parsedColumn) ? parsedColumn : 0;
                var rendererError = $"RENDERER ERROR · {message} · line {line}:{column}";
                SetStatus(rendererError);
                WriteRendererRuntimeStatus(rendererError);
                return;
            }
            if (string.Equals(messageType, "rendererReady", StringComparison.Ordinal))
            {
                var rendererSystem = root.TryGetProperty("system", out var rendererSystemProperty) ? rendererSystemProperty.GetString() ?? "?" : "?";
                var entityCount = root.TryGetProperty("entities", out var entityCountProperty) && entityCountProperty.TryGetInt32(out var parsedEntities) ? parsedEntities : 0;
                var bodyCount = root.TryGetProperty("bodies", out var bodyCountProperty) && bodyCountProperty.TryGetInt32(out var parsedBodies) ? parsedBodies : 0;
                var webGl = root.TryGetProperty("webgl", out var webGlProperty) && webGlProperty.ValueKind == JsonValueKind.True;
                var visiblePlanets = root.TryGetProperty("visiblePlanets", out var visiblePlanetsProperty) && visiblePlanetsProperty.TryGetInt32(out var parsedVisiblePlanets) ? parsedVisiblePlanets : -1;
                var visibleMoons = root.TryGetProperty("visibleMoons", out var visibleMoonsProperty) && visibleMoonsProperty.TryGetInt32(out var parsedVisibleMoons) ? parsedVisibleMoons : -1;
                var visibleStations = root.TryGetProperty("visibleStations", out var visibleStationsProperty) && visibleStationsProperty.TryGetInt32(out var parsedVisibleStations) ? parsedVisibleStations : -1;
                var nyxFieldParticles = root.TryGetProperty("nyxFieldParticles", out var nyxFieldParticlesProperty) && nyxFieldParticlesProperty.TryGetInt32(out var parsedNyxFieldParticles) ? parsedNyxFieldParticles : -1;
                var planetOrbits = root.TryGetProperty("planetOrbits", out var planetOrbitsProperty) && planetOrbitsProperty.TryGetInt32(out var parsedPlanetOrbits) ? parsedPlanetOrbits : -1;
                var visibility = visiblePlanets >= 0 && visibleMoons >= 0 ? $" · visible {visiblePlanets} planet(s) / {visibleMoons} moon(s)" : string.Empty;
                var stations = visibleStations >= 0 ? $" · {visibleStations} station marker(s)" : string.Empty;
                var field = nyxFieldParticles > 0 ? $" · {nyxFieldParticles} Nyx field particles" : string.Empty;
                var orbits = planetOrbits >= 0 ? $" · {planetOrbits} planet orbit(s)" : string.Empty;
                var rendererReady = $"{rendererSystem.ToUpperInvariant()} · renderer ready · {entityCount:N0} entities · {bodyCount:N0} bodies{visibility}{stations}{field}{orbits} · {(webGl ? "WebGL2 active" : "Canvas fallback")}";
                SetStatus(rendererReady);
                WriteRendererRuntimeStatus(rendererReady);
                if (_pendingGalaxySearchFocus is { } pending &&
                    pending.System.Equals(rendererSystem, StringComparison.OrdinalIgnoreCase))
                {
                    _pendingGalaxySearchFocus = null;
                    await FocusGalaxySearchResultInRendererAsync(pending);
                }
                return;
            }
            if (string.Equals(messageType, "rendererTextureStatus", StringComparison.Ordinal))
            {
                var rendererSystem = root.TryGetProperty("system", out var rendererSystemProperty) ? rendererSystemProperty.GetString() ?? "?" : "?";
                var texturedBodies = root.TryGetProperty("texturedBodies", out var texturedProperty) && texturedProperty.TryGetInt32(out var parsedTextured) ? parsedTextured : 0;
                var renderableBodies = root.TryGetProperty("renderableBodies", out var renderableProperty) && renderableProperty.TryGetInt32(out var parsedRenderable) ? parsedRenderable : 0;
                var overviewLoaded = root.TryGetProperty("overviewLoaded", out var overviewProperty) && overviewProperty.TryGetInt32(out var parsedOverview) ? parsedOverview : 0;
                var fullLoaded = root.TryGetProperty("fullLoaded", out var fullProperty) && fullProperty.TryGetInt32(out var parsedFull) ? parsedFull : 0;
                var textureEntries = root.TryGetProperty("textureEntries", out var entriesProperty) && entriesProperty.TryGetInt32(out var parsedEntries) ? parsedEntries : 0;
                WriteRendererTextureStatus($"{rendererSystem.ToUpperInvariant()} · textured {texturedBodies}/{renderableBodies} renderable bodies · overview loaded {overviewLoaded} · full loaded {fullLoaded} · payload entries {textureEntries}");
                return;
            }
            if (string.Equals(messageType, "systemSwitchRequest", StringComparison.Ordinal))
            {
                var requestedSystem = root.TryGetProperty("system", out var systemProperty) ? systemProperty.GetString() : null;
                if (!string.IsNullOrWhiteSpace(requestedSystem) && _datasets.ContainsKey(requestedSystem))
                {
                    SystemComboBox.SelectedItem = _datasets.Keys.First(k => k.Equals(requestedSystem, StringComparison.OrdinalIgnoreCase));
                }
                return;
            }

            if (string.Equals(messageType, "displaySettingsUpdate", StringComparison.Ordinal))
            {
                bool ReadDisplayBool(string name, bool fallback) =>
                    root.TryGetProperty(name, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? property.GetBoolean()
                        : fallback;

                _displaySettings = _displaySettings with
                {
                    ShowStars = ReadDisplayBool("showStars", _displaySettings.ShowStars),
                    ShowPlanets = ReadDisplayBool("showPlanets", _displaySettings.ShowPlanets),
                    ShowStations = ReadDisplayBool("showStations", _displaySettings.ShowStations),
                    ShowSystemStations = ReadDisplayBool("showSystemStations", _displaySettings.ShowSystemStations),
                    ShowMissionStations = ReadDisplayBool("showMissionStations", _displaySettings.ShowMissionStations),
                    ExternalFreightElevatorsOnly = ReadDisplayBool("externalFreightElevatorsOnly", _displaySettings.ExternalFreightElevatorsOnly),
                    SurfaceFreightElevatorsOnly = ReadDisplayBool("surfaceFreightElevatorsOnly", _displaySettings.SurfaceFreightElevatorsOnly),
                    SurfaceVehicleServicesOnly = ReadDisplayBool("surfaceVehicleServicesOnly", _displaySettings.SurfaceVehicleServicesOnly),
                    SurfaceGaragesOnly = ReadDisplayBool("surfaceGaragesOnly", _displaySettings.SurfaceGaragesOnly),
                    SurfaceLandingPadsOnly = ReadDisplayBool("surfaceLandingPadsOnly", _displaySettings.SurfaceLandingPadsOnly),
                    ShowRacingTracks = ReadDisplayBool("showRacingTracks", _displaySettings.ShowRacingTracks),
                    ShowJumpPoints = ReadDisplayBool("showJumpPoints", _displaySettings.ShowJumpPoints),
                    ShowOrbits = ReadDisplayBool("showOrbits", _displaySettings.ShowOrbits),
                    ShowAsteroids = ReadDisplayBool("showAsteroids", _displaySettings.ShowAsteroids),
                    ShowLabels = ReadDisplayBool("showLabels", _displaySettings.ShowLabels),
                    ShowRegions = ReadDisplayBool("showRegions", _displaySettings.ShowRegions),
                    ShowHidden = ReadDisplayBool("hidden", _displaySettings.ShowHidden),
                    BackgroundStars = ReadDisplayBool("backgroundStars", _displaySettings.BackgroundStars),
                    MajorOnly = ReadDisplayBool("majorOnly", _displaySettings.MajorOnly),
                    Guides = ReadDisplayBool("guides", _displaySettings.Guides),
                    WebGlBodies = ReadDisplayBool("webglBodies", _displaySettings.WebGlBodies),
                    OrbitPlane = ReadDisplayBool("orbitPlane", _displaySettings.OrbitPlane),
                    SurveyLighting = ReadDisplayBool("surveyLighting", _displaySettings.SurveyLighting),
                    SurveyNightFilter = ReadDisplayBool("surveyNightFilter", _displaySettings.SurveyNightFilter)
                };
                await _displaySettingsService.SaveAsync(_displaySettings, _loadCts?.Token ?? CancellationToken.None);
                return;
            }

            if (string.Equals(messageType, "applicationSettingsUpdate", StringComparison.Ordinal))
            {
                bool ReadBool(string name, bool fallback) =>
                    root.TryGetProperty(name, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? property.GetBoolean()
                        : fallback;
                string? ReadString(string name, string? fallback) =>
                    root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
                        ? NullIfWhiteSpace(property.GetString())
                        : fallback;

                var consent = ReadBool("onlineLocationEnrichmentConsent", _settings.OnlineLocationEnrichmentConsent);
                var onlineEnabled = ReadBool("onlineLocationEnrichmentEnabled", _settings.OnlineLocationEnrichmentEnabled) && consent;
                _settings = _settings with
                {
                    LocalP4kUpdateAdapterEnabled = ReadBool("localP4kUpdateAdapterEnabled", _settings.LocalP4kUpdateAdapterEnabled),
                    ScUnpackedImportEnabled = ReadBool("scUnpackedImportEnabled", _settings.ScUnpackedImportEnabled),
                    OnlineLocationEnrichmentConsent = consent,
                    OnlineLocationEnrichmentEnabled = onlineEnabled,
                    StarBreakerPath = ReadString("starBreakerPath", _settings.StarBreakerPath),
                    DataP4kPath = ReadString("dataP4kPath", _settings.DataP4kPath),
                    ScUnpackedRoot = ReadString("scUnpackedRoot", _settings.ScUnpackedRoot),
                    SyncHostEnabled = ReadBool("syncHostEnabled", _settings.SyncHostEnabled),
                    SyncHostBaseUrl = ReadString("syncHostBaseUrl", _settings.SyncHostBaseUrl),
                    SyncHostTransportMode = ReadString("syncHostTransportMode", _settings.SyncHostTransportMode) ?? "auto",
                    SyncHostPlayerHandle = ReadString("syncHostPlayerHandle", _settings.SyncHostPlayerHandle),
                    SyncHostOrganization = ReadString("syncHostOrganization", _settings.SyncHostOrganization),
                    UiLanguage = UiLocalizationCatalog.NormalizeLanguage(ReadString("uiLanguage", _settings.UiLanguage))
                };
                await _settingsService.SaveAsync(_settings, _loadCts?.Token ?? CancellationToken.None);
                ApplySettingsUiState();
                MapView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
                {
                    type = "applicationSettingsSaved",
                    settings = _settings,
                    onlineActive = _settings.OnlineLocationEnrichmentActive
                }));

                var reload = root.TryGetProperty("reload", out var reloadProperty) && reloadProperty.ValueKind == JsonValueKind.True;
                if (reload) await ReloadAsync();
                return;
            }

            if (string.Equals(messageType, "syncHostProbeRequest", StringComparison.Ordinal))
            {
                var probe = await _syncHostClientService.ProbeAsync(_settings, _loadCts?.Token ?? CancellationToken.None);
                MapView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
                {
                    type = "syncHostProbeResult",
                    success = probe.Success,
                    manifestAvailable = probe.ManifestAvailable,
                    manifestHttpStatus = probe.ManifestHttpStatus,
                    registryHttpStatus = probe.RegistryHttpStatus,
                    registryState = probe.RegistryState,
                    message = probe.Message,
                    clientId = probe.ClientId,
                    fingerprint = probe.Fingerprint
                }));
                return;
            }

            if (string.Equals(messageType, "supportDiagnosticsRequest", StringComparison.Ordinal))
            {
                var result = await _supportDiagnosticsService.RunAsync(
                    _settings,
                    _syncHostClientService,
                    _syncHostIdentityService,
                    _loadCts?.Token ?? CancellationToken.None);
                MapView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
                {
                    type = "supportDiagnosticsResult",
                    overallStatus = result.OverallStatus,
                    logPath = result.LogPath,
                    packagePath = result.PackagePath,
                    checks = result.Checks.Select(x => new
                    {
                        area = x.Area,
                        name = x.Name,
                        status = x.Status,
                        message = x.Message,
                        path = x.Path,
                        version = x.Version,
                        sizeBytes = x.SizeBytes
                    }).ToArray()
                }));
                return;
            }

            if (string.Equals(messageType, "clearOnlineEnrichmentCache", StringComparison.Ordinal))
            {
                if (Directory.Exists(_locationImageCache.RootPath))
                {
                    foreach (var file in Directory.EnumerateFiles(_locationImageCache.RootPath, "*", SearchOption.TopDirectoryOnly))
                    {
                        try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                    }
                }
                MapView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "onlineEnrichmentCacheCleared" }));
                return;
            }

            if (string.Equals(messageType, "locationImageRequest", StringComparison.Ordinal))
            {
                var requestKey = root.TryGetProperty("requestKey", out var requestKeyProperty) ? requestKeyProperty.GetString() ?? string.Empty : string.Empty;
                var locationUuid = root.TryGetProperty("uuid", out var uuidProperty) && uuidProperty.ValueKind == JsonValueKind.String ? uuidProperty.GetString() : null;
                var locationName = root.TryGetProperty("name", out var locationNameProperty) ? locationNameProperty.GetString() ?? string.Empty : string.Empty;
                if (!_settings.OnlineLocationEnrichmentActive)
                {
                    MapView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
                    {
                        type = "locationImageResult",
                        requestKey,
                        uuid = locationUuid ?? string.Empty,
                        name = locationName,
                        exists = false,
                        url = (string?)null,
                        sourceUrl = (string?)null,
                        provider = "DISABLED",
                        status = "ONLINE_ENRICHMENT_DISABLED_OR_NOT_CONSENTED"
                    }));
                    return;
                }
                var asset = await _locationImageCache.GetOrDownloadAsync(locationUuid, locationName, _loadCts?.Token ?? CancellationToken.None);
                MapView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
                {
                    type = "locationImageResult",
                    requestKey,
                    uuid = asset.LocationUuid,
                    name = asset.LocationName,
                    exists = asset.Exists,
                    url = asset.BrowserAssetUrl,
                    sourceUrl = asset.SourceUrl,
                    provider = asset.Provider,
                    status = asset.DataStatus,
                    description = asset.Description,
                    details = asset.Details,
                    sourceVersion = asset.SourceVersion,
                    sourceUpdatedAt = asset.SourceUpdatedAt
                }));
                return;
            }

            if (string.Equals(messageType, "locationOverrideImageSetRequest", StringComparison.Ordinal))
            {
                string ReadString(string name) => root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
                    ? property.GetString() ?? string.Empty
                    : string.Empty;
                var key = ReadString("key");
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select local location image",
                    Filter = "Image files|*.png;*.jpg;*.jpeg;*.webp|PNG|*.png|JPEG|*.jpg;*.jpeg|WebP|*.webp",
                    CheckFileExists = true,
                    Multiselect = false
                };
                if (dialog.ShowDialog(this) != true) return;
                var record = await _locationUserOverrideService.SetImageAsync(
                    key,
                    ReadString("name"),
                    NullIfWhiteSpace(ReadString("locationUuid")),
                    NullIfWhiteSpace(ReadString("placementId")),
                    NullIfWhiteSpace(ReadString("surfaceAnchorId")),
                    dialog.FileName,
                    _loadCts?.Token ?? CancellationToken.None);
                PostLocationOverrideResult(key, record, "LOCAL_IMAGE_SAVED");
                return;
            }

            if (string.Equals(messageType, "locationOverrideImageDeleteRequest", StringComparison.Ordinal))
            {
                var key = root.TryGetProperty("key", out var keyProperty) ? keyProperty.GetString() ?? string.Empty : string.Empty;
                var record = await _locationUserOverrideService.DeleteImageAsync(key, _loadCts?.Token ?? CancellationToken.None);
                PostLocationOverrideResult(key, record, "LOCAL_IMAGE_DELETED");
                return;
            }

            if (string.Equals(messageType, "locationOverrideDescriptionSetRequest", StringComparison.Ordinal))
            {
                string ReadString(string name) => root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
                    ? property.GetString() ?? string.Empty
                    : string.Empty;
                var key = ReadString("key");
                var record = await _locationUserOverrideService.SetDescriptionAsync(
                    key,
                    ReadString("name"),
                    NullIfWhiteSpace(ReadString("locationUuid")),
                    NullIfWhiteSpace(ReadString("placementId")),
                    NullIfWhiteSpace(ReadString("surfaceAnchorId")),
                    ReadString("description"),
                    _loadCts?.Token ?? CancellationToken.None);
                PostLocationOverrideResult(key, record, "LOCAL_DESCRIPTION_SAVED");
                return;
            }

            if (string.Equals(messageType, "locationOverrideDescriptionDeleteRequest", StringComparison.Ordinal))
            {
                var key = root.TryGetProperty("key", out var keyProperty) ? keyProperty.GetString() ?? string.Empty : string.Empty;
                var record = await _locationUserOverrideService.DeleteDescriptionAsync(key, _loadCts?.Token ?? CancellationToken.None);
                PostLocationOverrideResult(key, record, "LOCAL_DESCRIPTION_DELETED");
                return;
            }

            if (string.Equals(messageType, "navigationState", StringComparison.Ordinal))
            {
                _navigationStart = ReadNavigationTarget(root, "start");
                _navigationDestination = ReadNavigationTarget(root, "destination");
                return;
            }

            if (string.Equals(messageType, "bookmarkGroupRequest", StringComparison.Ordinal))
            {
                if (_bookmarkGroupService is null) return;
                var action = root.TryGetProperty("action", out var actionProperty) ? actionProperty.GetString() ?? string.Empty : string.Empty;
                Guid[] ReadBookmarkIds() => root.TryGetProperty("bookmarkIds", out var idsProperty) && idsProperty.ValueKind == JsonValueKind.Array
                    ? idsProperty.EnumerateArray().Select(x => Guid.TryParse(x.GetString(), out var id) ? id : Guid.Empty).Where(x => x != Guid.Empty).Distinct().ToArray()
                    : [];
                switch (action)
                {
                    case "create":
                    {
                        var name = root.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() ?? string.Empty : string.Empty;
                        var description = root.TryGetProperty("description", out var descriptionProperty) ? descriptionProperty.GetString() : null;
                        await _bookmarkGroupService.CreateAsync(name, description);
                        break;
                    }
                    case "update":
                    {
                        if (!root.TryGetProperty("id", out var idProperty) || !Guid.TryParse(idProperty.GetString(), out var id)) break;
                        var name = root.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() ?? string.Empty : string.Empty;
                        var description = root.TryGetProperty("description", out var descriptionProperty) ? descriptionProperty.GetString() : null;
                        await _bookmarkGroupService.UpdateMetadataAsync(id, name, description);
                        break;
                    }
                    case "delete":
                    {
                        if (root.TryGetProperty("id", out var idProperty) && Guid.TryParse(idProperty.GetString(), out var id))
                            await _bookmarkGroupService.DeleteAsync(id);
                        break;
                    }
                    case "assign":
                    {
                        if (root.TryGetProperty("id", out var idProperty) && Guid.TryParse(idProperty.GetString(), out var id))
                            await _bookmarkGroupService.AssignAsync(id, ReadBookmarkIds());
                        break;
                    }
                    case "remove":
                    {
                        if (root.TryGetProperty("id", out var idProperty) && Guid.TryParse(idProperty.GetString(), out var id))
                            await _bookmarkGroupService.RemoveAsync(id, ReadBookmarkIds());
                        break;
                    }
                    case "move":
                    {
                        if (root.TryGetProperty("sourceId", out var sourceProperty) && Guid.TryParse(sourceProperty.GetString(), out var sourceId) &&
                            root.TryGetProperty("targetId", out var targetProperty) && Guid.TryParse(targetProperty.GetString(), out var targetId))
                            await _bookmarkGroupService.MoveAsync(sourceId, targetId, ReadBookmarkIds());
                        break;
                    }
                    case "publish":
                    {
                        if (root.TryGetProperty("id", out var idProperty) && Guid.TryParse(idProperty.GetString(), out var id))
                        {
                            var package = await _bookmarkGroupService.BuildPackageAsync(id);
                            MapView.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
                            {
                                type = "bookmarkGroupPublishPrepared",
                                groupId = package.Group.Id,
                                groupName = package.Group.Name,
                                fingerprint = package.ContentFingerprint,
                                bookmarkCount = package.Bookmarks.Count,
                                status = _settings.SyncHostEnabled && !string.IsNullOrWhiteSpace(_settings.SyncHostBaseUrl)
                                    ? "SIGNED_PACKAGE_READY_SYNC_HOST_MODULE_PENDING"
                                    : "SIGNED_PACKAGE_READY_SYNC_HOST_NOT_CONFIGURED"
                            }));
                        }
                        break;
                    }
                }
                await PostBookmarkStateAsync();
                return;
            }

            if (string.Equals(messageType, "bookmarkBatchRequest", StringComparison.Ordinal))
            {
                if (_bookmarkService is null) return;
                var action = root.TryGetProperty("action", out var actionProperty) ? actionProperty.GetString() ?? string.Empty : string.Empty;
                var ids = root.TryGetProperty("ids", out var idsProperty) && idsProperty.ValueKind == JsonValueKind.Array
                    ? idsProperty.EnumerateArray().Select(x => Guid.TryParse(x.GetString(), out var id) ? id : Guid.Empty).Where(x => x != Guid.Empty).Distinct().ToArray()
                    : [];
                if (action == "delete")
                {
                    foreach (var id in ids) await _bookmarkService.DeleteAsync(id);
                }
                else if (action == "update")
                {
                    var current = (await _bookmarkService.ListAsync()).ToDictionary(x => x.Id);
                    var category = root.TryGetProperty("category", out var categoryProperty) ? categoryProperty.GetString() : null;
                    bool? showOnMap = root.TryGetProperty("showOnMap", out var showProperty) && showProperty.ValueKind is JsonValueKind.True or JsonValueKind.False ? showProperty.GetBoolean() : null;
                    foreach (var id in ids)
                    {
                        if (!current.TryGetValue(id, out var bookmark)) continue;
                        await _bookmarkService.UpdateMetadataAsync(
                            id,
                            bookmark.Name,
                            bookmark.Tags,
                            bookmark.Visibility,
                            category ?? bookmark.Category,
                            bookmark.Note,
                            bookmark.Color,
                            showOnMap ?? bookmark.ShowOnMap,
                            bookmark.BookmarkScope);
                    }
                }
                await PostBookmarkStateAsync();
                return;
            }

            if (string.Equals(messageType, "bookmarkRequest", StringComparison.Ordinal))
            {
                if (_bookmarkService is null) return;
                var action = root.TryGetProperty("action", out var actionProperty) ? actionProperty.GetString() ?? string.Empty : string.Empty;
                switch (action)
                {
                    case "createPlacement":
                    {
                        var system = root.GetProperty("system").GetString() ?? string.Empty;
                        var placementId = root.GetProperty("placementId").GetString() ?? string.Empty;
                        var name = root.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() : null;
                        await _bookmarkService.CreatePlacementAsync(system, placementId, name);
                        break;
                    }
                    case "createSurface":
                    {
                        var system = root.GetProperty("system").GetString() ?? string.Empty;
                        var anchorId = root.GetProperty("anchorId").GetString() ?? string.Empty;
                        var name = root.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() : null;
                        await _bookmarkService.CreateSurfaceAnchorAsync(system, anchorId, name);
                        break;
                    }
                    case "createSystemPoint":
                    {
                        var system = root.GetProperty("system").GetString() ?? string.Empty;
                        var name = root.GetProperty("name").GetString() ?? string.Empty;
                        var x = root.GetProperty("x").GetDouble();
                        var y = root.GetProperty("y").GetDouble();
                        var z = root.GetProperty("z").GetDouble();
                        await _bookmarkService.CreateSystemPointAsync(name, system, new StarSyncUniverse.Transforms.Vector3D(x, y, z));
                        break;
                    }
                    case "createContext":
                    {
                        var system = root.GetProperty("system").GetString() ?? string.Empty;
                        var name = root.GetProperty("name").GetString() ?? string.Empty;
                        var coordinateKind = root.TryGetProperty("coordinateKind", out var kindProperty) ? kindProperty.GetString() ?? "SYSTEM_XYZ" : "SYSTEM_XYZ";
                        var placementId = root.TryGetProperty("placementId", out var placementProperty) && placementProperty.ValueKind == JsonValueKind.String ? placementProperty.GetString() : null;
                        var surfaceAnchorId = root.TryGetProperty("surfaceAnchorId", out var anchorProperty) && anchorProperty.ValueKind == JsonValueKind.String ? anchorProperty.GetString() : null;
                        var bodyName = root.TryGetProperty("bodyName", out var bodyNameProperty) && bodyNameProperty.ValueKind == JsonValueKind.String ? bodyNameProperty.GetString() : null;
                        var bodySourceUuid = root.TryGetProperty("bodySourceUuid", out var bodyUuidProperty) && bodyUuidProperty.ValueKind == JsonValueKind.String ? bodyUuidProperty.GetString() : null;
                        double? ReadOptionalDouble(string propertyName) => root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number ? property.GetDouble() : null;
                        StarSyncUniverse.Domain.BookmarkRecord created;
                        if (!string.IsNullOrWhiteSpace(surfaceAnchorId))
                            created = await _bookmarkService.CreateSurfaceAnchorAsync(system, surfaceAnchorId, name);
                        else if (!string.IsNullOrWhiteSpace(placementId))
                            created = await _bookmarkService.CreatePlacementAsync(system, placementId, name);
                        else if (coordinateKind.Equals("BODY_FIXED_XYZ", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(bodyName))
                            created = await _bookmarkService.CreateBodyFixedPointAsync(name, system, bodyName,
                                new StarSyncUniverse.Transforms.Vector3D(root.GetProperty("x").GetDouble(), root.GetProperty("y").GetDouble(), root.GetProperty("z").GetDouble()),
                                bodySourceUuid, ReadOptionalDouble("latitude"), ReadOptionalDouble("longitude"), ReadOptionalDouble("altitude"));
                        else
                            created = await _bookmarkService.CreateSystemPointAsync(name, system,
                                new StarSyncUniverse.Transforms.Vector3D(root.GetProperty("x").GetDouble(), root.GetProperty("y").GetDouble(), root.GetProperty("z").GetDouble()));

                        var category = root.TryGetProperty("category", out var categoryProperty) ? categoryProperty.GetString() : null;
                        var note = root.TryGetProperty("note", out var noteProperty) ? noteProperty.GetString() : null;
                        var color = root.TryGetProperty("color", out var colorProperty) ? colorProperty.GetString() : null;
                        var showOnMap = root.TryGetProperty("showOnMap", out var showProperty) && showProperty.ValueKind is JsonValueKind.True or JsonValueKind.False ? showProperty.GetBoolean() : true;
                        var scope = root.TryGetProperty("bookmarkScope", out var scopeProperty) ? scopeProperty.GetString() : null;
                        await _bookmarkService.UpdateMetadataAsync(created.Id, name, created.Tags, created.Visibility, category, note, color, showOnMap, scope);
                        break;
                    }
                    case "update":
                    {
                        if (!root.TryGetProperty("id", out var idProperty) || !Guid.TryParse(idProperty.GetString(), out var id)) break;
                        var name = root.GetProperty("name").GetString() ?? string.Empty;
                        var visibility = root.TryGetProperty("visibility", out var visibilityProperty) ? visibilityProperty.GetString() ?? "PRIVATE" : "PRIVATE";
                        var tags = root.TryGetProperty("tags", out var tagsProperty) && tagsProperty.ValueKind == JsonValueKind.Array
                            ? tagsProperty.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!).ToArray()
                            : [];
                        var category = root.TryGetProperty("category", out var categoryProperty) ? categoryProperty.GetString() : null;
                        var note = root.TryGetProperty("note", out var noteProperty) ? noteProperty.GetString() : null;
                        var color = root.TryGetProperty("color", out var colorProperty) ? colorProperty.GetString() : null;
                        bool? showOnMap = root.TryGetProperty("showOnMap", out var showProperty) && showProperty.ValueKind is JsonValueKind.True or JsonValueKind.False ? showProperty.GetBoolean() : null;
                        var scope = root.TryGetProperty("bookmarkScope", out var scopeProperty) ? scopeProperty.GetString() : null;
                        await _bookmarkService.UpdateMetadataAsync(id, name, tags, visibility, category, note, color, showOnMap, scope);
                        break;
                    }
                    case "delete":
                    {
                        if (root.TryGetProperty("id", out var idProperty) && Guid.TryParse(idProperty.GetString(), out var id))
                            await _bookmarkService.DeleteAsync(id);
                        break;
                    }
                }

                await PostBookmarkStateAsync();
                return;
            }

            if (!string.Equals(messageType, "routeRequest", StringComparison.Ordinal))
                return;

            var requestId = root.TryGetProperty("requestId", out var rid) ? rid.GetInt32() : 0;
            var startTarget = ReadNavigationTarget(root, "start");
            var destinationTarget = ReadNavigationTarget(root, "destination");
            if (startTarget is null || destinationTarget is null) return;
            if (!_datasets.ContainsKey(startTarget.System) || !_datasets.ContainsKey(destinationTarget.System)) return;

            if (_routePlanner is null) return;
            var route = _routePlanner.FindRoute(startTarget, destinationTarget);
            var routeSummary = NavigationRouteAnalyzer.Analyze(route);

            var payload = JsonSerializer.Serialize(new
            {
                type = "routeResult",
                requestId,
                found = route.Found,
                status = route.DataStatus,
                measurableDistanceMeters = route.MeasurableInSystemDistanceMeters,
                segmentCount = routeSummary.SegmentCount,
                inSystemSegmentCount = routeSummary.InSystemSegmentCount,
                interstellarTransitCount = routeSummary.InterstellarTransitCount,
                perInterstellarTransitQuantumFuelFraction = routeSummary.PerInterstellarTransitQuantumFuelFraction,
                quantumFuelAggregationStatus = routeSummary.QuantumFuelAggregationStatus,
                routeSummaryStatus = routeSummary.DataStatus,
                legs = route.Legs.Select(leg => new
                {
                    legType = leg.LegType,
                    system = leg.System,
                    fromLabel = leg.FromLabel,
                    toLabel = leg.ToLabel,
                    fromX = leg.FromX,
                    fromY = leg.FromY,
                    fromZ = leg.FromZ,
                    toX = leg.ToX,
                    toY = leg.ToY,
                    toZ = leg.ToZ,
                    distanceMeters = leg.DistanceMeters,
                    dataStatus = leg.DataStatus
                })
            });
            MapView.CoreWebView2.PostWebMessageAsJson(payload);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Route WebView message failed: " + ex.Message);
        }
    }

    private static StarSyncUniverse.Domain.NavigationTarget? ReadNavigationTarget(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        var system = element.TryGetProperty("system", out var systemProperty) ? systemProperty.GetString() ?? string.Empty : string.Empty;
        var id = element.TryGetProperty("id", out var idProperty) ? idProperty.GetString() : null;
        var name = element.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() ?? id ?? string.Empty : id ?? string.Empty;
        double? ReadNumber(string name) => element.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : null;
        var status = element.TryGetProperty("status", out var statusProperty) ? statusProperty.GetString() ?? "LOCAL_RUNTIME_SELECTION" : "LOCAL_RUNTIME_SELECTION";
        var coordinateKind = element.TryGetProperty("coordinateKind", out var coordinateKindProperty)
            ? coordinateKindProperty.GetString() ?? "PLACEMENT_SNAPSHOT_XYZ"
            : "PLACEMENT_SNAPSHOT_XYZ";
        var surfaceAnchorId = element.TryGetProperty("surfaceAnchorId", out var surfaceAnchorProperty) ? surfaceAnchorProperty.GetString() : null;
        var bodyName = element.TryGetProperty("bodyName", out var bodyNameProperty) ? bodyNameProperty.GetString() : null;
        return new StarSyncUniverse.Domain.NavigationTarget(
            system,
            name,
            id,
            ReadNumber("x"),
            ReadNumber("y"),
            ReadNumber("z"),
            coordinateKind,
            status,
            surfaceAnchorId,
            bodyName);
    }

    private static void WriteRendererRuntimeStatus(string message)
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StarSyncUniverse", "renderer-runtime-status.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, $"{DateTimeOffset.Now:O} {message}");
        }
        catch
        {
            // Diagnostics must never interfere with renderer operation.
        }
    }

    private static void WriteRendererTextureStatus(string message)
    {
        try
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StarSyncUniverse", "renderer-texture-status.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, $"{DateTimeOffset.Now:O} {message}");
        }
        catch
        {
            // Diagnostics must never interfere with renderer operation.
        }
    }

    private void SetStatus(string message) => StatusText.Text = $"StarSyncUniverse {AppVersion} · {message}";
}

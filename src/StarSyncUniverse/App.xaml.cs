using System.Windows;
using StarSyncUniverse.Data;
using StarSyncUniverse.Services;

namespace StarSyncUniverse;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Any(a => a.Equals("--headless-location-image-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StarSyncUniverse", "headless-location-image-smoke.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            try
            {
                var cache = new LocationImageCacheService();
                var asset = await cache.GetOrDownloadAsync("567f92d7-d34c-42fa-b57b-f1939d4c5f5b", "Orison", CancellationToken.None);
                await File.WriteAllLinesAsync(logPath,
                [
                    asset.Exists ? "HEADLESS_LOCATION_IMAGE_OK" : "HEADLESS_LOCATION_IMAGE_FAIL",
                    $"name={asset.LocationName}",
                    $"provider={asset.Provider}",
                    $"source={asset.SourceUrl}",
                    $"local={asset.LocalPath}",
                    $"browser={asset.BrowserAssetUrl}",
                    $"status={asset.DataStatus}"
                ]);
                Shutdown(asset.Exists ? 0 : 3);
            }
            catch (Exception ex)
            {
                await File.WriteAllTextAsync(logPath, "HEADLESS_LOCATION_IMAGE_FAIL\n" + ex);
                Shutdown(2);
            }
            return;
        }

        if (e.Args.Any(a => a.Equals("--headless-export-community-baseline", StringComparison.OrdinalIgnoreCase)))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StarSyncUniverse", "headless-community-baseline-export.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            try
            {
                var repoRoot = RepositoryLocator.FindRepositoryRoot()
                    ?? throw new DirectoryNotFoundException("StarSync repository root could not be located.");
                var scRoot = RepositoryLocator.FindLatestScUnpacked(repoRoot)
                    ?? throw new DirectoryNotFoundException("No SCUnpacked dataset containing starmap_positions.json was found below /database.");
                var knowledge = ScUnpackedKnowledgeDatabase.Load(scRoot);
                var manifest = await CommunityBaselineExporter.ExportAsync(repoRoot, knowledge, CancellationToken.None);
                await File.WriteAllLinesAsync(logPath,
                [
                    "HEADLESS_COMMUNITY_BASELINE_EXPORT_OK",
                    $"manifest={manifest}",
                    $"locations={knowledge.Summary.LocationCount}",
                    $"factions={knowledge.Summary.FactionCount}",
                    $"services={knowledge.Summary.ServiceCount}",
                    $"commodityLocations={knowledge.Summary.CommodityRelationLocationCount}",
                    $"commodities={knowledge.Summary.CommodityCount}",
                    $"itemsIndexedAtFreeze={knowledge.Summary.ItemCount}"
                ]);
                Shutdown(0);
            }
            catch (Exception ex)
            {
                await File.WriteAllTextAsync(logPath, "HEADLESS_COMMUNITY_BASELINE_EXPORT_FAIL\n" + ex);
                Shutdown(2);
            }
            return;
        }

        if (e.Args.Any(a => a.Equals("--headless-bookmark-group-smoke", StringComparison.OrdinalIgnoreCase)))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StarSyncUniverse", "headless-bookmark-group-smoke.log");
            var tempRoot = Path.Combine(Path.GetTempPath(), "StarSyncUniverse-BookmarkSmoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            try
            {
                var identityService = new UniverseSyncHostClientIdentityService();
                var sourceStore = new LocalBookmarkStore(Path.Combine(tempRoot, "source-bookmarks.json"), identityService);
                var sourceGroups = new BookmarkGroupService(sourceStore, identityService, Path.Combine(tempRoot, "source-groups.json"));
                var bookmarkId = Guid.NewGuid();
                var now = DateTimeOffset.UtcNow;
                var bookmark = new StarSyncUniverse.Domain.BookmarkRecord(
                    bookmarkId,
                    "Smoke Bookmark",
                    "stanton",
                    "frame:stanton:system",
                    "SYSTEM_XYZ",
                    1000,
                    2000,
                    3000,
                    null,
                    null,
                    null,
                    null,
                    ["smoke"],
                    "PRIVATE",
                    1,
                    now,
                    now,
                    "Infos",
                    "Signed bookmark group smoke test",
                    "#f59e0b",
                    true,
                    "SPACE");
                var storedBookmark = await sourceStore.UpsertAsync(bookmark);
                var group = await sourceGroups.CreateAsync("Smoke Group", "Signature and stable-ID verification");
                group = await sourceGroups.AssignAsync(group.Id, [storedBookmark.Id]);
                var package = await sourceGroups.BuildPackageAsync(group.Id);

                var targetStore = new LocalBookmarkStore(Path.Combine(tempRoot, "target-bookmarks.json"), identityService);
                var targetGroups = new BookmarkGroupService(targetStore, identityService, Path.Combine(tempRoot, "target-groups.json"));
                var imported = await targetGroups.ImportPackageAsync(package);
                await targetGroups.ImportPackageAsync(package);
                var targetBookmarks = await targetStore.LoadAsync();
                var targetGroupList = await targetGroups.ListAsync();
                var signatureInput = string.Join("\n", new[]
                {
                    "starsync-universe-bookmark-group-v1",
                    package.Group.Id.ToString("D"),
                    package.Group.CreatorClientId ?? string.Empty,
                    package.PublisherClientId ?? string.Empty,
                    package.ContentFingerprint ?? string.Empty,
                    package.ExportedUtc.ToUniversalTime().ToString("O")
                });
                var signatureValid = UniverseSyncHostClientIdentityService.Verify(
                    package.SignerPublicKeySpkiBase64,
                    System.Text.Encoding.UTF8.GetBytes(signatureInput),
                    package.Signature);
                var ok = signatureValid
                         && targetBookmarks.Count == 1
                         && targetBookmarks[0].Id == bookmarkId
                         && targetBookmarks[0].CreatorClientId == storedBookmark.CreatorClientId
                         && targetGroupList.Count == 1
                         && targetGroupList[0].Id == group.Id
                         && targetGroupList[0].CreatorClientId == group.CreatorClientId
                         && imported.Id == group.Id
                         && !string.IsNullOrWhiteSpace(package.ContentFingerprint);
                await File.WriteAllLinesAsync(logPath,
                [
                    ok ? "HEADLESS_BOOKMARK_GROUP_OK" : "HEADLESS_BOOKMARK_GROUP_FAIL",
                    $"groupId={group.Id:D}",
                    $"bookmarkId={bookmarkId:D}",
                    $"creator={storedBookmark.CreatorClientId}",
                    $"groupCreator={group.CreatorClientId}",
                    $"fingerprint={package.ContentFingerprint}",
                    $"signatureAlgorithm={package.SignatureAlgorithm}",
                    $"signatureValid={signatureValid}",
                    $"targetBookmarkCount={targetBookmarks.Count}",
                    $"targetGroupCount={targetGroupList.Count}"
                ]);
                Shutdown(ok ? 0 : 3);
            }
            catch (Exception ex)
            {
                await File.WriteAllTextAsync(logPath, "HEADLESS_BOOKMARK_GROUP_FAIL\n" + ex);
                Shutdown(2);
            }
            finally
            {
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true); } catch { }
            }
            return;
        }

        if (e.Args.Any(a => a.Equals("--headless-import", StringComparison.OrdinalIgnoreCase)))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "StarSyncUniverse", "headless-import.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            try
            {
                var repoRoot = RepositoryLocator.FindRepositoryRoot()
                    ?? throw new DirectoryNotFoundException("StarSync repository root could not be located.");
                var scRoot = RepositoryLocator.FindLatestScUnpacked(repoRoot)
                    ?? throw new DirectoryNotFoundException("No SCUnpacked dataset containing starmap_positions.json was found below /database.");
                var catalog = ScUnpackedCatalog.Load(Path.Combine(scRoot, "starmap_positions.json"));
                var knowledge = ScUnpackedKnowledgeDatabase.Load(scRoot);
                var starBreakerPath = Environment.GetEnvironmentVariable("STARSYNC_STARBREAKER")
                    ?? Path.Combine(AppContext.BaseDirectory, "starbreaker.exe");
                var dataP4k = Environment.GetEnvironmentVariable("STARSYNC_DATA_P4K")
                    ?? throw new InvalidOperationException("STARSYNC_DATA_P4K must point to the Star Citizen Data.p4k for headless import.");
                var coordinator = new UniverseImportCoordinator(new StarBreakerClient(starBreakerPath, dataP4k), catalog);
                var progressLines = new List<string>();
                var progress = new Progress<string>(message => progressLines.Add($"{DateTimeOffset.UtcNow:O} {message}"));
                var datasets = await coordinator.ImportAllAsync(progress, CancellationToken.None);
                var knowledgeAudit = await knowledge.WriteAuditAsync(datasets, CancellationToken.None);
                progressLines.Add($"{DateTimeOffset.UtcNow:O} Knowledge correlation audit: {knowledgeAudit} · locations={knowledge.Summary.LocationCount}; factions={knowledge.Summary.FactionCount}; services={knowledge.Summary.ServiceCount}; commodityLocations={knowledge.Summary.CommodityRelationLocationCount}; commodities={knowledge.Summary.CommodityCount}; items={knowledge.Summary.ItemCount}");
                var summary = datasets.Values
                    .OrderBy(x => x.System, StringComparer.OrdinalIgnoreCase)
                    .Select(x => $"{x.System}: entities={x.Entities.Count}; validation={x.Diagnostics.LastOrDefault(d => d.StartsWith("Validation:", StringComparison.Ordinal))}");
                await File.WriteAllLinesAsync(logPath,
                    progressLines.Concat(["HEADLESS_IMPORT_OK", .. summary]));
                Shutdown(0);
            }
            catch (Exception ex)
            {
                await File.WriteAllTextAsync(logPath, "HEADLESS_IMPORT_FAIL\n" + ex);
                Shutdown(2);
            }
            return;
        }

        ShutdownMode = ShutdownMode.OnMainWindowClose;
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}

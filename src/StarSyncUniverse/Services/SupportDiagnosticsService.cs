using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StarSyncUniverse.Localization;

namespace StarSyncUniverse.Services;

public sealed record SupportCheckItem(
    string Area,
    string Name,
    string Status,
    string Message,
    string? Path = null,
    string? Version = null,
    long? SizeBytes = null);

public sealed record SupportDiagnosticsResult(
    DateTimeOffset TimestampUtc,
    string ApplicationVersion,
    string OverallStatus,
    IReadOnlyList<SupportCheckItem> Checks,
    string LogPath,
    string PackagePath);

/// <summary>
/// Read-only integrity/support diagnostics. The package never includes the SyncHost private key,
/// bookmark contents or raw Data.p4k/SCUnpacked payloads. It contains only the generated report,
/// redacted configuration metadata and small runtime status files.
/// </summary>
public sealed class SupportDiagnosticsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<SupportDiagnosticsResult> RunAsync(
        StarSyncUniverseSettings settings,
        UniverseSyncHostClientService syncHostClient,
        UniverseSyncHostClientIdentityService identityService,
        CancellationToken cancellationToken = default)
    {
        var checks = new List<SupportCheckItem>();
        var appRoot = AppContext.BaseDirectory;
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";

        CheckApplicationIntegrity(appRoot, checks);
        await CheckStarBreakerAsync(settings, checks, cancellationToken);
        CheckScUnpacked(settings, checks);
        await CheckSyncHostAsync(settings, syncHostClient, checks, cancellationToken);

        var identity = identityService.GetOrCreate();
        checks.Add(new SupportCheckItem(
            "SyncHost", "Client identity", "OK",
            $"Client ID {identity.ClientId}; fingerprint {identity.PublicKeyFingerprint}; algorithm {identity.SignatureAlgorithm}. Private key is not exported."));

        var overall = checks.Any(x => x.Status is "ERROR" or "CORRUPT")
            ? "ERROR"
            : checks.Any(x => x.Status is "WARN" or "MISSING" or "NOT CONFIGURED") ? "WARN" : "OK";

        var supportRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StarSyncUniverse", "Support");
        Directory.CreateDirectory(supportRoot);
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var logPath = Path.Combine(supportRoot, $"StarSyncUniverse-support-{stamp}.log");
        var packagePath = Path.Combine(supportRoot, $"StarSyncUniverse-support-{stamp}.zip");

        var report = new SupportDiagnosticsResult(
            DateTimeOffset.UtcNow, version, overall, checks, logPath, packagePath);
        await File.WriteAllTextAsync(logPath, BuildTextReport(report), Encoding.UTF8, cancellationToken);
        await CreateSupportPackageAsync(report, settings, identity, packagePath, cancellationToken);
        return report;
    }

    private static void CheckApplicationIntegrity(string appRoot, List<SupportCheckItem> checks)
    {
        var criticalFiles = new[]
        {
            "StarSyncUniverse.exe",
            "StarSyncUniverse.dll",
            "StarSyncUniverse.Contracts.dll",
            Path.Combine("Assets", "Branding", "starsyncuniverse_boot.jpg"),
            Path.Combine("Assets", "Branding", "starsyncuniverse_about.jpg"),
            Path.Combine("Assets", "CommunityBaseline", "Knowledge", "location-knowledge.json")
        };

        foreach (var relative in criticalFiles)
        {
            var path = Path.Combine(appRoot, relative);
            if (!File.Exists(path))
            {
                checks.Add(new SupportCheckItem("Tool", relative, "CORRUPT", "Required application file is missing.", path));
                continue;
            }

            try
            {
                var info = new FileInfo(path);
                if (info.Length <= 0)
                    throw new InvalidDataException("File is empty.");

                if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    _ = AssemblyName.GetAssemblyName(path);
                else if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    var version = FileVersionInfo.GetVersionInfo(path);
                    if (string.IsNullOrWhiteSpace(version.FileVersion) && string.IsNullOrWhiteSpace(version.ProductVersion))
                        throw new InvalidDataException("Executable version metadata is missing.");
                    using var stream = File.OpenRead(path);
                    _ = stream.ReadByte();
                }
                else if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    using var stream = File.OpenRead(path);
                    using var _ = JsonDocument.Parse(stream);
                }
                else
                {
                    using var stream = File.OpenRead(path);
                    _ = stream.ReadByte();
                }

                var hash = ComputeSha256(path);
                checks.Add(new SupportCheckItem("Tool", relative, "OK", $"Readable and structurally valid. SHA-256 {hash}.", path, null, info.Length));
            }
            catch (Exception ex)
            {
                checks.Add(new SupportCheckItem("Tool", relative, "CORRUPT", ex.Message, path));
            }
        }
    }

    private static async Task CheckStarBreakerAsync(
        StarSyncUniverseSettings settings,
        List<SupportCheckItem> checks,
        CancellationToken cancellationToken)
    {
        var exe = settings.StarBreakerPath;
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
        {
            checks.Add(new SupportCheckItem("StarBreaker", "Installation", "MISSING", "starbreaker.exe was not found. Copy it into the application directory or configure a path.", exe));
            return;
        }

        var info = FileVersionInfo.GetVersionInfo(exe);
        var version = FirstNonEmpty(info.ProductVersion, info.FileVersion, "unknown");
        checks.Add(new SupportCheckItem("StarBreaker", "Installation", "OK", "Executable found.", exe, version, new FileInfo(exe).Length));

        var requiredCommands = new[]
        {
            new[] { "--help" },
            new[] { "p4k", "extract", "--help" },
            new[] { "dcb", "query", "--help" }
        };
        foreach (var args in requiredCommands)
        {
            var name = string.Join(' ', args);
            var result = await RunProcessProbeAsync(exe, args, settings.DataP4kPath, TimeSpan.FromSeconds(20), cancellationToken);
            checks.Add(new SupportCheckItem(
                "StarBreaker", $"Command {name}", result.ExitCode == 0 ? "OK" : "ERROR",
                result.ExitCode == 0 ? "Command is available." : $"Exit {result.ExitCode}: {Trim(result.Error, 320)}",
                exe, version));
        }

        var p4k = settings.DataP4kPath;
        if (string.IsNullOrWhiteSpace(p4k) || !File.Exists(p4k))
        {
            checks.Add(new SupportCheckItem("StarBreaker", "Data.p4k", "MISSING", "Configured Star Citizen LIVE Data.p4k was not found.", p4k));
            return;
        }

        try
        {
            var fi = new FileInfo(p4k);
            using var fs = new FileStream(p4k, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            _ = fs.ReadByte();
            checks.Add(new SupportCheckItem("StarBreaker", "Data.p4k", "OK", $"Readable; last modified {fi.LastWriteTimeUtc:O}. Full-file hashing is intentionally skipped for performance.", p4k, null, fi.Length));
        }
        catch (Exception ex)
        {
            checks.Add(new SupportCheckItem("StarBreaker", "Data.p4k", "ERROR", ex.Message, p4k));
        }
    }

    private static void CheckScUnpacked(StarSyncUniverseSettings settings, List<SupportCheckItem> checks)
    {
        var root = settings.ScUnpackedRoot;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            checks.Add(new SupportCheckItem("SCUnpacked", "Installation", "MISSING", "SCUnpacked root was not found. Copy the dataset below application/database or configure a path.", root));
            return;
        }

        var version = DetectScUnpackedVersion(root);
        checks.Add(new SupportCheckItem("SCUnpacked", "Installation", "OK", "Dataset root found.", root, version));

        var required = new[]
        {
            "starmap.json",
            "starmap_positions.json",
            "trade_locations.json",
            "items.json",
            Path.Combine("resources", "commodities.json"),
            Path.Combine("resources", "commodity_trade_locations.json")
        };

        foreach (var relative in required)
        {
            var path = Path.Combine(root, relative);
            if (!File.Exists(path))
            {
                var mandatory = relative.Equals("starmap.json", StringComparison.OrdinalIgnoreCase) || relative.Equals("starmap_positions.json", StringComparison.OrdinalIgnoreCase);
                checks.Add(new SupportCheckItem("SCUnpacked", relative, mandatory ? "ERROR" : "WARN", mandatory ? "Required dataset object is missing." : "Optional enrichment object is missing; related metadata will be incomplete.", path, version));
                continue;
            }

            try
            {
                var fi = new FileInfo(path);
                using var stream = File.OpenRead(path);
                using var json = JsonDocument.Parse(stream);
                var kind = json.RootElement.ValueKind.ToString();
                checks.Add(new SupportCheckItem("SCUnpacked", relative, "OK", $"Valid JSON ({kind}).", path, version, fi.Length));
            }
            catch (Exception ex)
            {
                checks.Add(new SupportCheckItem("SCUnpacked", relative, "CORRUPT", ex.Message, path, version));
            }
        }

        var factions = Path.Combine(root, "factions");
        var factionCount = Directory.Exists(factions) ? Directory.EnumerateFiles(factions, "*.json", SearchOption.TopDirectoryOnly).Count() : 0;
        checks.Add(new SupportCheckItem("SCUnpacked", "factions", factionCount > 0 ? "OK" : "WARN", factionCount > 0 ? $"{factionCount} faction definition(s) found." : "No faction definitions found.", factions, version));

        try
        {
            var db = ScUnpackedKnowledgeDatabase.Load(root);
            checks.Add(new SupportCheckItem("SCUnpacked", "Semantic load validation", "OK", $"Knowledge database loaded: {db.Summary.LocationCount} locations, {db.Summary.FactionCount} factions, {db.Summary.ItemCount} items, {db.Summary.TradeLocationCount} trade locations.", root, version));
        }
        catch (Exception ex)
        {
            checks.Add(new SupportCheckItem("SCUnpacked", "Semantic load validation", "ERROR", ex.Message, root, version));
        }
    }

    private static async Task CheckSyncHostAsync(
        StarSyncUniverseSettings settings,
        UniverseSyncHostClientService syncHostClient,
        List<SupportCheckItem> checks,
        CancellationToken cancellationToken)
    {
        if (!settings.SyncHostEnabled || string.IsNullOrWhiteSpace(settings.SyncHostBaseUrl))
        {
            checks.Add(new SupportCheckItem("SyncHost", "Connection", "NOT CONFIGURED", "SyncHost is disabled or no base URL is configured.", settings.SyncHostBaseUrl));
            return;
        }

        var probe = await syncHostClient.ProbeAsync(settings, cancellationToken);
        checks.Add(new SupportCheckItem(
            "SyncHost", "Connection", probe.Success ? "OK" : "ERROR",
            $"Manifest HTTP {probe.ManifestHttpStatus}; registry HTTP {probe.RegistryHttpStatus}; state {probe.RegistryState}; {probe.Message}",
            settings.SyncHostBaseUrl));
    }

    private static async Task<(int ExitCode, string Output, string Error)> RunProcessProbeAsync(
        string executable,
        IReadOnlyList<string> args,
        string? dataP4k,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            if (!string.IsNullOrWhiteSpace(dataP4k)) psi.Environment["SC_DATA_P4K"] = dataP4k;
            foreach (var arg in args) psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Process start failed.");
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            using var cancellationRegistration = timeoutCts.Token.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch { }
            });
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token);
            return (process.ExitCode, await stdoutTask, await stderrTask);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (-2, string.Empty, "Command probe timed out.");
        }
        catch (Exception ex)
        {
            return (-1, string.Empty, ex.Message);
        }
    }

    private static async Task CreateSupportPackageAsync(
        SupportDiagnosticsResult report,
        StarSyncUniverseSettings settings,
        UniverseSyncHostClientIdentity identity,
        string packagePath,
        CancellationToken cancellationToken)
    {
        if (File.Exists(packagePath)) File.Delete(packagePath);
        using var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create);

        await AddTextAsync(archive, "support-report.log", BuildTextReport(report), cancellationToken);
        await AddTextAsync(archive, "support-report.json", JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        var safeSettings = new
        {
            settings.SchemaVersion,
            settings.LocalP4kUpdateAdapterEnabled,
            settings.ScUnpackedImportEnabled,
            settings.OnlineLocationEnrichmentEnabled,
            settings.OnlineLocationEnrichmentConsent,
            StarBreakerPath = settings.StarBreakerPath,
            DataP4kPath = settings.DataP4kPath,
            ScUnpackedRoot = settings.ScUnpackedRoot,
            settings.SyncHostEnabled,
            settings.SyncHostBaseUrl,
            settings.SyncHostTransportMode,
            settings.SyncHostPlayerHandle,
            settings.SyncHostOrganization,
            settings.UiLanguage,
            SyncHostClientId = identity.ClientId,
            SyncHostFingerprint = identity.PublicKeyFingerprint,
            SyncHostSignatureAlgorithm = identity.SignatureAlgorithm,
            Note = "Private SyncHost key material is intentionally excluded."
        };
        await AddTextAsync(archive, "configuration.json", JsonSerializer.Serialize(safeSettings, JsonOptions), cancellationToken);

        var localRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StarSyncUniverse");
        foreach (var name in new[] { "renderer-runtime-status.txt", "renderer-texture-status.txt" })
        {
            var path = Path.Combine(localRoot, name);
            if (File.Exists(path)) archive.CreateEntryFromFile(path, name, CompressionLevel.Optimal);
        }
    }

    private static async Task AddTextAsync(ZipArchive archive, string name, string content, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        await writer.WriteAsync(content.AsMemory(), cancellationToken);
    }

    private static string BuildTextReport(SupportDiagnosticsResult report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("StarSyncUniverse Support Diagnostics");
        sb.AppendLine($"Timestamp UTC: {report.TimestampUtc:O}");
        sb.AppendLine($"Application: {report.ApplicationVersion}");
        sb.AppendLine($"Overall: {report.OverallStatus}");
        sb.AppendLine();
        foreach (var group in report.Checks.GroupBy(x => x.Area))
        {
            sb.AppendLine($"[{group.Key}]");
            foreach (var item in group)
            {
                sb.AppendLine($"{item.Status,-14} {item.Name}: {item.Message}");
                if (!string.IsNullOrWhiteSpace(item.Path)) sb.AppendLine($"  Path: {item.Path}");
                if (!string.IsNullOrWhiteSpace(item.Version)) sb.AppendLine($"  Version: {item.Version}");
                if (item.SizeBytes is { } size) sb.AppendLine($"  Size: {size} bytes");
            }
            sb.AppendLine();
        }
        sb.AppendLine($"Log: {report.LogPath}");
        sb.AppendLine($"Support package: {report.PackagePath}");
        return sb.ToString();
    }

    private static string DetectScUnpackedVersion(string root)
    {
        foreach (var file in new[] { "version.txt", "VERSION", "build.txt" })
        {
            var path = Path.Combine(root, file);
            if (!File.Exists(path)) continue;
            try
            {
                var text = File.ReadAllText(path).Trim();
                if (text.Length > 0) return Trim(text, 120);
            }
            catch { }
        }
        var positions = Path.Combine(root, "starmap_positions.json");
        var name = new DirectoryInfo(root).Name;
        return File.Exists(positions) ? $"{name} / {File.GetLastWriteTimeUtc(positions):yyyy-MM-dd}" : name;
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string Trim(string? value, int max)
    {
        var text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return text.Length <= max ? text : text[..max] + "...";
    }

    private static string FirstNonEmpty(params string?[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;
}

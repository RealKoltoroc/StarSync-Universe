using System.Diagnostics;
using System.Text.Json;

namespace StarSyncUniverse.Data;

/// <summary>
/// Reads SSolarSystem from the CURRENT LIVE DataCore through StarBreaker.
/// The galacticPosition values are kept exactly as game-record coordinates;
/// no light-year/unit conversion is asserted until the DataCore unit semantics are proven.
/// </summary>
public sealed class SolarSystemDataCoreImporter
{
    private readonly StarBreakerClient _starBreaker;

    public SolarSystemDataCoreImporter(StarBreakerClient starBreaker) => _starBreaker = starBreaker;

    public async Task<SolarSystemDataCoreRecord?> ImportAsync(string system, CancellationToken cancellationToken = default)
    {
        if (!_starBreaker.IsAvailable)
            return null;

        var proper = char.ToUpperInvariant(system[0]) + system[1..].ToLowerInvariant();
        var psi = new ProcessStartInfo
        {
            FileName = _starBreaker.Executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.Environment["SC_DATA_P4K"] = _starBreaker.DataP4k;
        psi.ArgumentList.Add("dcb");
        psi.ArgumentList.Add("query");
        psi.ArgumentList.Add("SSolarSystem");
        psi.ArgumentList.Add("--filter");
        psi.ArgumentList.Add($"*{proper}*");

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start StarBreaker DataCore query.");
        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch { }
        });
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"StarBreaker SSolarSystem query failed ({process.ExitCode}): {stderr}");

        using var json = JsonDocument.Parse(stdout);
        var root = json.RootElement;
        var value = root.GetProperty("_RecordValue_");
        var position = value.GetProperty("galacticPosition");
        var name = value.TryGetProperty("Name", out var n) ? n.GetString() ?? proper : proper;
        var recordId = root.TryGetProperty("_RecordId_", out var id) ? id.GetString() : null;
        var solarSystemRecord = value.TryGetProperty("SolarSystemRecord", out var sr) ? sr.GetString() : null;
        var defaultLocation = value.TryGetProperty("DefaultLocation", out var dl) ? dl.GetString() : null;

        return new SolarSystemDataCoreRecord(
            name,
            recordId,
            position.GetProperty("x").GetDouble(),
            position.GetProperty("y").GetDouble(),
            position.GetProperty("z").GetDouble(),
            solarSystemRecord,
            defaultLocation,
            "Data.p4k CURRENT LIVE DataCore SSolarSystem.galacticPosition",
            "LOCAL_DIRECT_DATACORE_UNIT_SEMANTICS_UNRESOLVED");
    }
}

public sealed record SolarSystemDataCoreRecord(
    string System,
    string? RecordId,
    double GalaxyX,
    double GalaxyY,
    double GalaxyZ,
    string? SolarSystemRecord,
    string? DefaultLocation,
    string SourceAuthority,
    string DataStatus);

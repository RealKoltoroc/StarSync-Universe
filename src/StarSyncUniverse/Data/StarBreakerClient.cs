using System.Diagnostics;

namespace StarSyncUniverse.Data;

public sealed class StarBreakerClient
{
    public StarBreakerClient(string executable, string dataP4k)
    {
        Executable = executable;
        DataP4k = dataP4k;
    }

    public string Executable { get; }
    public string DataP4k { get; }

    public bool IsAvailable => File.Exists(Executable) && File.Exists(DataP4k);

    public async Task<string> ExtractAsync(string filter, string cacheName, CancellationToken cancellationToken = default, string? convert = null)
    {
        if (!IsAvailable)
            throw new FileNotFoundException($"StarBreaker/Data.p4k unavailable. StarBreaker={Executable}; Data.p4k={DataP4k}");

        if (cacheName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            cacheName.Contains(Path.DirectorySeparatorChar) ||
            cacheName.Contains(Path.AltDirectorySeparatorChar) ||
            cacheName is "." or "..")
            throw new ArgumentException("Cache name must be a single safe directory name.", nameof(cacheName));

        var output = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StarSyncUniverse", "Extracts", cacheName);
        var sourceInfo = new FileInfo(DataP4k);
        var fingerprint = $"{sourceInfo.Length}|{sourceInfo.LastWriteTimeUtc.Ticks}|{filter}|convert={convert ?? "none"}";
        var fingerprintPath = Path.Combine(output, ".source-fingerprint");

        if (Directory.Exists(output) && File.Exists(fingerprintPath) &&
            string.Equals(await File.ReadAllTextAsync(fingerprintPath, cancellationToken), fingerprint, StringComparison.Ordinal))
        {
            return output;
        }

        if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        Directory.CreateDirectory(output);

        var psi = new ProcessStartInfo
        {
            FileName = Executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.Environment["SC_DATA_P4K"] = DataP4k;
        psi.ArgumentList.Add("p4k");
        psi.ArgumentList.Add("extract");
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(output);
        psi.ArgumentList.Add("--filter");
        psi.ArgumentList.Add(filter);
        if (!string.IsNullOrWhiteSpace(convert) && !convert.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            psi.ArgumentList.Add("--convert");
            psi.ArgumentList.Add(convert);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start StarBreaker.");
        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch { }
        });
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"StarBreaker failed ({process.ExitCode}). {await stderr}");

        _ = await stdout;
        await File.WriteAllTextAsync(fingerprintPath, fingerprint, cancellationToken);
        return output;
    }
}

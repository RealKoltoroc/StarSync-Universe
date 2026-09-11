namespace StarSyncUniverse.Services;

public static class RepositoryLocator
{
    public static string? FindRepositoryRoot()
    {
        var candidates = new[]
        {
            AppContext.BaseDirectory,
            Environment.CurrentDirectory
        };

        foreach (var candidate in candidates)
        {
            var dir = new DirectoryInfo(candidate);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "StarSyncUniverse.slnx")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "database")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
        }

        return null;
    }

    public static string? FindLatestScUnpacked(string repositoryRoot)
    {
        var databaseRoot = Path.Combine(repositoryRoot, "database");
        if (!Directory.Exists(databaseRoot)) return null;

        return Directory.EnumerateFiles(databaseRoot, "starmap_positions.json", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => new DirectoryInfo(path!))
            .OrderByDescending(d => d.LastWriteTimeUtc)
            .Select(d => d.FullName)
            .FirstOrDefault();
    }
}

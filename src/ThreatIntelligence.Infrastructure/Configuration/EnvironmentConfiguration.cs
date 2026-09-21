using DotNetEnv;

namespace ThreatIntelligence.Infrastructure.Configuration;

public static class EnvironmentConfiguration
{
    public static string? FindDotEnvPath()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ".env");
            if (File.Exists(candidate)) return candidate;

            directory = directory.Parent;
        }

        return null;
    }

    public static void LoadDotEnv()
    {
        var candidate = FindDotEnvPath();
        if (candidate is not null)
        {
            Env.Load(candidate, LoadOptions.NoClobber());
            return;
        }

        Env.Load(options: LoadOptions.NoClobber());
    }

    public static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path)) return path;

        return Path.Combine(
            Path.GetDirectoryName(FindDotEnvPath() ?? string.Empty) ?? Directory.GetCurrentDirectory(),
            path);
    }
}

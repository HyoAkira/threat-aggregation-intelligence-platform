using DotNetEnv;

namespace ThreatIntelligence.Infrastructure.Configuration;

public static class EnvironmentConfiguration
{
    public static void LoadDotEnv()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, ".env");
            if (File.Exists(candidate))
            {
                Env.Load(candidate, LoadOptions.NoClobber());
                return;
            }

            directory = directory.Parent;
        }

        Env.Load(options: LoadOptions.NoClobber());
    }
}

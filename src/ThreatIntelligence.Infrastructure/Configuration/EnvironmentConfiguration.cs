using DotNetEnv;

namespace ThreatIntelligence.Infrastructure.Configuration;

public static class EnvironmentConfiguration
{
    public static void LoadDotEnv() => Env.Load();
}

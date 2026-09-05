namespace ThreatIntelligence.Infrastructure.ConnectionStrings;

public static class DatabaseConnectionStringResolver
{
    public const string PasswordToken = "__DB_PASSWORD__";

    public static string? Resolve(string? configuredConnectionString, string? password)
    {
        if (string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return configuredConnectionString;
        }

        return configuredConnectionString.Replace(PasswordToken, password ?? string.Empty);
    }
}
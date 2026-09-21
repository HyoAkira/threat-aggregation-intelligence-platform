using Microsoft.Extensions.Configuration;
using System.Text;

namespace ThreatIntelligence.Infrastructure.Configuration;

public sealed record ThreatIntelligenceOptions(
    DatabaseOptions Database,
    RabbitMqOptions RabbitMq,
    JwtOptions Jwt,
    PasswordHashOptions PasswordHash,
    ThreatFoxOptions ThreatFox,
    PhishTankOptions PhishTank,
    EncryptionOptions Encryption,
    OpenAiCompatibleOptions OpenAiCompatible,
    AdminSeedOptions AdminSeed,
    LoggingOptions Logging)
{
    public static ThreatIntelligenceOptions BindAndValidate(IConfiguration configuration)
    {
        var options = new ThreatIntelligenceOptions(
            new DatabaseOptions(Required(configuration, "Database:ConnectionString")),
            new RabbitMqOptions(
                Required(configuration, "RabbitMq:Host"),
                IntegerOrDefault(configuration, "RabbitMq:Port", 5672),
                Required(configuration, "RabbitMq:User"),
                Required(configuration, "RabbitMq:Password"),
                Required(configuration, "RabbitMq:Exchange")),
            new JwtOptions(
                LoadRequiredFile(configuration, "Jwt:PrivateKeyFile"),
                Required(configuration, "Jwt:Issuer"),
                Required(configuration, "Jwt:Audience"),
                IntegerOrDefault(configuration, "Jwt:ExpiryMinutes", 480)),
            new PasswordHashOptions(IntegerOrDefault(configuration, "Auth:PasswordWorkFactor", 12)),
            new ThreatFoxOptions(Optional(configuration, "ThreatFox:ApiKey")),
            new PhishTankOptions(Optional(configuration, "PhishTank:AppKey")),
            EncryptionOptions.FromConfiguration(configuration),
            new OpenAiCompatibleOptions(Optional(configuration, "OpenAICompatible:BaseUrl")),
            new AdminSeedOptions(
                Required(configuration, "Admin:Username"),
                Required(configuration, "Admin:Password")),
            new LoggingOptions(Required(configuration, "Logging:MinimumLevel")));

        options.Validate();
        return options;
    }

    private static string Required(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Required configuration value '{key}' is missing.");

    private static string? Optional(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value ? value : null;

    private static string LoadRequiredFile(IConfiguration configuration, string key)
    {
        var configuredPath = Required(configuration, key);
        var path = EnvironmentConfiguration.ResolvePath(configuredPath);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Required configuration file '{configuredPath}' for '{key}' was not found.");
        }

        return File.ReadAllText(path);
    }
    private static int IntegerOrDefault(IConfiguration configuration, string key, int defaultValue) =>
        configuration[key] is not { Length: > 0 } value
            ? defaultValue
            : int.TryParse(value, out var parsed)
                ? parsed
                : throw new InvalidOperationException($"Configuration value '{key}' must be an integer.");

    private void Validate()
    {
        if (RabbitMq.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException("RabbitMq:Port must be between 1 and 65535.");
        }

        if (Jwt.ExpiryMinutes != 480)
        {
            throw new InvalidOperationException("Jwt:ExpiryMinutes must be fixed at 480 minutes.");
        }

        if (PasswordHash.WorkFactor is < 4 or > 31)
        {
            throw new InvalidOperationException("Auth:PasswordWorkFactor must be between 4 and 31.");
        }
    }
}

public sealed record DatabaseOptions(string ConnectionString);

public sealed record RabbitMqOptions(string Host, int Port, string User, string Password, string Exchange);

public sealed record JwtOptions(string PrivateKey, string Issuer, string Audience, int ExpiryMinutes);

public sealed record PasswordHashOptions(int WorkFactor);

public sealed record ThreatFoxOptions(string? ApiKey = null);

public sealed record OpenAiCompatibleOptions(string? BaseUrl = null);

public sealed record AdminSeedOptions(string Username, string Password);

public sealed record LoggingOptions(string MinimumLevel);

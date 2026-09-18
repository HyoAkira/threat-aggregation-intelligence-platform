using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThreatIntelligence.Application.Abstractions.Authentication;
using ThreatIntelligence.Application.Abstractions.Hashing;
using ThreatIntelligence.Application.Abstractions.Persistence;
using ThreatIntelligence.Application.BatchWriter;
using ThreatIntelligence.Application.AI;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Application.Abstractions.Secrets;
using ThreatIntelligence.Application.Normalization;
using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Infrastructure.Admin;
using ThreatIntelligence.Infrastructure.Configuration;
using ThreatIntelligence.Infrastructure.Fetches;
using ThreatIntelligence.Infrastructure.Authentication;
using ThreatIntelligence.Infrastructure.Hashing;
using ThreatIntelligence.Infrastructure.HealthChecks;
using ThreatIntelligence.Infrastructure.Persistence;
using ThreatIntelligence.Infrastructure.Persistence.Repositories;
using ThreatIntelligence.Infrastructure.Persistence.Seeding;
using ThreatIntelligence.Infrastructure.Persistence.RateLimiting;
using ThreatIntelligence.Infrastructure.Pipeline;
using ThreatIntelligence.Infrastructure.PublicSuffix;
using ThreatIntelligence.Infrastructure.PhishTank;
using ThreatIntelligence.Infrastructure.Reporting;
using ThreatIntelligence.Infrastructure.Storage;
using ThreatIntelligence.Infrastructure.ThreatFox;
using ThreatIntelligence.Infrastructure.Secrets;
using ThreatIntelligence.Infrastructure.AI;

namespace ThreatIntelligence.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = ThreatIntelligenceOptions.BindAndValidate(configuration);

        services.AddSingleton(options);
        services.AddSingleton(options.Database);
        services.AddSingleton(options.RabbitMq);
        services.AddSingleton(options.Jwt);
        services.AddSingleton(options.PasswordHash);
        services.AddSingleton(options.ThreatFox);
        services.AddSingleton(options.PhishTank);
        services.AddSingleton(options.Encryption);
        services.AddSingleton(options.OpenAiCompatible);
        services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
        services.AddScoped<ISecretStore, EncryptedSecretStore>();
        services.AddScoped<IProviderCredentialResolver, ProviderCredentialResolver>();
        services.AddScoped<ISecretRotationService, SecretRotationService>();
        services.AddSingleton(options.AdminSeed);
        services.AddSingleton(options.Logging);

        services.AddDbContext<ThreatIntelligenceDbContext>(
            db => db.UseSqlServer(options.Database.ConnectionString),
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Singleton);
        services.AddDbContextFactory<ThreatIntelligenceDbContext>(db =>
            db.UseSqlServer(options.Database.ConnectionString));

        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<ProviderSeeder>();
        services.AddScoped<AdminSeeder>();
        services.AddScoped<LegacySecretImporter>();
        services.AddScoped<ThreatIntelligence.Infrastructure.AI.AiReportPolicySeeder>();
        services.AddSingleton(AiClientOptions.Default);
        services.AddSingleton<AiReportThrottle>();
        var pandocPath = configuration["Pdf:PandocPath"] is { Length: > 0 } configuredPandocPath
            ? configuredPandocPath
            : "pandoc";
        var templatePath = configuration["Pdf:TemplatePath"] is { Length: > 0 } configuredTemplatePath
            ? configuredTemplatePath
            : Path.Combine(AppContext.BaseDirectory, "Pdf", "eisvogel.latex");
        services.AddSingleton<IMarkdownPdfRenderer>(serviceProvider =>
            new MarkdownPdfRenderer(
                pandocPath,
                templatePath,
                serviceProvider.GetRequiredService<ILogger<MarkdownPdfRenderer>>()));
        services.AddScoped<ThreatIntelligence.Application.AI.IAiConfigurationReader, AiConfigurationReader>();
        services.AddScoped<ThreatIntelligence.Application.AI.AiReportGenerator>();
        services.AddHttpClient<ThreatIntelligence.Application.AI.IChatCompletionClient, OpenAiCompatibleChatClient>(
            static client =>
            {
                // The per-request timeout is enforced around the whole streaming operation; the
                // handler timeout stays infinite so a healthy long stream is never aborted.
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .RemoveAllLoggers();

        services.AddScoped<IIocRepository, IocRepository>();
        services.AddScoped<IIocQueryReader, IocQueryReader>();
        services.AddScoped<IAdminProviderOperationsReader, AdminProviderOperationsReader>();
        services.AddScoped<IAdminProviderRepository, AdminProviderRepository>();
        services.AddScoped<IAdminSummaryReader, AdminSummaryReader>();
        services.AddSingleton(new AdminLogOptions(
            configuration["Admin:LogDirectory"] is { Length: > 0 } logDirectory ? logDirectory : "logs",
            configuration["Admin:LogFilePrefix"] is { Length: > 0 } logPrefix ? logPrefix : "api-",
            configuration["Admin:WorkerLogFilePrefix"] is { Length: > 0 } workerLogPrefix ? workerLogPrefix : "worker-",
            configuration["Admin:WorkerLogDirectory"]));
        services.AddSingleton<IAdminLogReader, AdminLogReader>();
        services.AddSingleton(new RawStorageOptions(
            configuration["RawStorage:BasePath"] is { Length: > 0 } rawStoragePath ? rawStoragePath : "data/providers"));
        services.AddSingleton<IRawFeedStore, RawFeedStore>();
        services.AddScoped<IUserAuthenticationReader, UserAuthenticationReader>();
        services.AddScoped<IFetchHistoryRepository, FetchHistoryRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IIocBatchWriter, IocBatchWriter>();
        services.AddSingleton<FalsePositiveEvaluator>();
        services.AddScoped<IFetchGuard, FetchGuard>();
        services.AddScoped<IFetchLifecycle, FetchLifecycle>();
        services.AddSingleton<IFetchLifecycleEventSink, LoggerFetchLifecycleEventSink>();
        services.AddScoped<IFetchJobPublisher, MassTransitFetchJobPublisher>();
        services.AddSingleton<FetchSchedulerService>();
        services.AddScoped<UnavailableProviderFetchExecutor>();
        services.AddHostedService<RuntimeLogLevelWatcher>();

        services.AddSingleton<IValueHasher, Sha256ValueHasher>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<IParentDomainResolver, PublicSuffixParentDomainResolver>();
        services.AddSingleton(TimeProvider.System);

        AddThreatFoxAdapter(services);
        AddPhishTankAdapter(services);
        RegisterProviderMaps(services);
        RegisterExecutorRouter(services);

        services.AddHealthChecks()
            .AddCheck<SqlServerHealthCheck>("sqlserver", tags: ["ready"])
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: ["ready"]);

        return services;
    }

    private static void AddThreatFoxAdapter(IServiceCollection services)
    {
        services.AddSingleton<ThreatFoxRecordMapper>();
        services.AddSingleton<ThreatFoxRecentParser>();
        services.AddSingleton<ThreatFoxFullParser>();
        services.AddSingleton<ThreatFoxProviderValidator>();
        services.AddSingleton<ThreatFoxUpsertFieldsMapper>();

        services.AddScoped<ThreatFoxFetchExecutor>();

        services.AddHttpClient<ThreatFoxExportClient>(configureClient: static client =>
        {
            // Explicit transport timeout; E05 owns retry policy, so the handler-level wiring never retries.
            // The ThreatFoxExportClient constructor sets the descriptive user agent.
            client.Timeout = TimeSpan.FromSeconds(100);
        })
        // The default HttpClient pipeline logs the absolute request URI, which contains the ThreatFox
        // API key. Remove those loggers so the key never reaches application logs; the client emits its
        // own key-free request/response logs.
        .RemoveAllLoggers();

        // Persist every received export before the executor parses it.
        services.AddScoped<IThreatFoxExportClient>(sp =>
            new PersistingThreatFoxExportClient(
                sp.GetRequiredService<ThreatFoxExportClient>(),
                sp.GetRequiredService<IRawFeedStore>()));
    }

    private static void AddPhishTankAdapter(IServiceCollection services)
    {
        services.AddSingleton<PhishTankRecordMapper>();
        services.AddSingleton<PhishTankFeedParser>();
        services.AddSingleton<PhishTankProviderValidator>();
        services.AddSingleton<PhishTankUpsertFieldsMapper>();
        services.AddScoped<PhishTankFetchExecutor>();

        services.AddHttpClient<PhishTankClient>(configureClient: static client =>
        {
            // Explicit transport timeout; E05 owns retry policy, so the handler-level wiring never retries.
            // The PhishTankClient constructor sets the descriptive user agent.
            client.Timeout = TimeSpan.FromMinutes(30);
        })
        // The default HttpClient pipeline logs the absolute request URI, which contains the PhishTank
        // application key when configured. Remove those loggers so the key never reaches application
        // logs; the client emits its own key-free request/response logs.
        .RemoveAllLoggers();

        // Persist the downloaded feed before the executor parses it.
        services.AddScoped<IPhishTankClient>(sp =>
            new PersistingPhishTankClient(
                sp.GetRequiredService<PhishTankClient>(),
                sp.GetRequiredService<IRawFeedStore>()));
    }

    private static void RegisterProviderMaps(IServiceCollection services)
    {
        services.AddSingleton<IReadOnlyDictionary<int, IIocUpsertFieldsMapper>>(sp =>
        {
            var threatFoxMapper = sp.GetRequiredService<ThreatFoxUpsertFieldsMapper>();
            var phishTankMapper = sp.GetRequiredService<PhishTankUpsertFieldsMapper>();
            return new Dictionary<int, IIocUpsertFieldsMapper>
            {
                [ThreatFoxRecordMapper.ThreatFoxProviderId] = threatFoxMapper,
                [PhishTankProviderValidator.PhishTankProviderId] = phishTankMapper
            };
        });

        services.AddSingleton<IReadOnlyDictionary<int, IProviderValidator>>(sp =>
        {
            var threatFoxValidator = sp.GetRequiredService<ThreatFoxProviderValidator>();
            var phishTankValidator = sp.GetRequiredService<PhishTankProviderValidator>();
            return new Dictionary<int, IProviderValidator>
            {
                [threatFoxValidator.ProviderId] = threatFoxValidator,
                [phishTankValidator.ProviderId] = phishTankValidator
            };
        });
    }

    private static void RegisterExecutorRouter(IServiceCollection services)
    {
        services.AddScoped<IProviderFetchExecutor>(sp =>
        {
            var threatFox = sp.GetRequiredService<ThreatFoxFetchExecutor>();
            var phishTank = sp.GetRequiredService<PhishTankFetchExecutor>();
            var fallback = sp.GetRequiredService<UnavailableProviderFetchExecutor>();
            var executors = new Dictionary<int, IProviderFetchExecutor>
            {
                [ThreatFoxRecordMapper.ThreatFoxProviderId] = threatFox,
                [PhishTankProviderValidator.PhishTankProviderId] = phishTank
            };
            return new ProviderFetchExecutorRouter(executors, fallback);
        });
    }
}

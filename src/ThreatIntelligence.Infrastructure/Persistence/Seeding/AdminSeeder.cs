using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Abstractions.Authentication;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Configuration;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.Infrastructure.Persistence.Seeding;

/// <summary>
/// Seeds the first administrator from configuration. Consumes an <see cref="IPasswordHasher"/>
/// abstraction (production KDF is provided by E04) and never logs or persists the plaintext
/// password. Idempotent, non-destructive, and converges under concurrent startup via the
/// unique username index.
/// </summary>
public sealed class AdminSeeder(
    ThreatIntelligenceDbContext dbContext,
    AdminSeedOptions options,
    TimeProvider timeProvider,
    IPasswordHasher hasher)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Username) || string.IsNullOrWhiteSpace(options.Password))
        {
            throw new InvalidOperationException(
                "First-admin seeding requires non-empty ADMIN__USERNAME and ADMIN__PASSWORD.");
        }

        if (options.Username.Length > 50)
        {
            throw new InvalidOperationException("ADMIN__USERNAME must not exceed 50 characters.");
        }

        var existing = await dbContext.Users
            .SingleOrDefaultAsync(u => u.Username == options.Username, cancellationToken);

        if (existing is not null)
        {
            if (existing.Role == UserRole.Admin)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Configuration conflict: username '{options.Username}' already exists and is not an Admin.");
        }

        var passwordHash = hasher.HashPassword(options.Password);
        dbContext.Users.Add(new User(
            options.Username,
            passwordHash,
            UserRole.Admin,
            timeProvider.GetUtcNow().UtcDateTime));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var winner = await dbContext.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.Username == options.Username, cancellationToken);

            if (winner is not null && winner.Role == UserRole.Admin)
            {
                return;
            }

            throw;
        }
    }
}

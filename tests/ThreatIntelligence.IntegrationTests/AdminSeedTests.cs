using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Abstractions.Authentication;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Configuration;
using ThreatIntelligence.Infrastructure.Persistence;
using ThreatIntelligence.Infrastructure.Persistence.Seeding;

namespace ThreatIntelligence.IntegrationTests;

/// <summary>
/// Deterministic test hasher that never reproduces the plaintext (so the plaintext-persistence
/// capture test can prove the password appears nowhere).
/// </summary>
public sealed class TestPasswordHasher : IPasswordHasher
{
    public string HashPassword(string password) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)));

    public bool VerifyPassword(string password, string? passwordHash) =>
        passwordHash is not null && HashPassword(password) == passwordHash;
}

[Collection("sqlserver")]
public class AdminSeedTests(SqlServerFixture fixture)
{
    private static readonly DateTime Utc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private readonly TestPasswordHasher _hasher = new();

    private AdminSeeder CreateSeeder(string username = "admin", string password = "S3cret-Pass!")
    {
        var context = fixture.CreateContext();
        return new AdminSeeder(context, new AdminSeedOptions(username, password), new FixedTimeProvider(Utc), _hasher);
    }

    private async Task ClearUsersAsync()
    {
        await using var context = fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM users");
    }

    [Fact]
    public async Task First_run_creates_exactly_one_admin_with_configured_username()
    {
        await ClearUsersAsync();
        await using var context = fixture.CreateContext();
        var seeder = CreateSeeder(username: "admin", password: "S3cret-Pass!");
        await seeder.SeedAsync(CancellationToken.None);

        var admin = await context.Users.SingleAsync(u => u.Username == "admin");
        Assert.Equal(UserRole.Admin, admin.Role);
        Assert.Equal(Utc, admin.CreatedAt);
        Assert.False(string.IsNullOrEmpty(admin.PasswordHash));
        Assert.Equal(1, await context.Users.CountAsync());
    }

    [Fact]
    public async Task Plaintext_password_appears_nowhere_in_database_values()
    {
        await ClearUsersAsync();
        const string password = "S3cret-Pass!";
        await using var context = fixture.CreateContext();
        var seeder = CreateSeeder(password: password);
        await seeder.SeedAsync(CancellationToken.None);

        var rows = await context.Users
            .Select(u => new { u.Username, u.PasswordHash })
            .ToListAsync();

        Assert.All(rows, row =>
        {
            Assert.DoesNotContain(password, row.Username);
            Assert.DoesNotContain(password, row.PasswordHash);
        });
    }

    [Fact]
    public async Task Reseeding_is_non_destructive_and_does_not_reset_credentials()
    {
        await ClearUsersAsync();
        await using var context = fixture.CreateContext();
        var seeder = CreateSeeder();
        await seeder.SeedAsync(CancellationToken.None);

        var originalHash = (await context.Users.SingleAsync(u => u.Username == "admin")).PasswordHash;

        await seeder.SeedAsync(CancellationToken.None);

        var after = await context.Users.SingleAsync(u => u.Username == "admin");
        Assert.Equal(originalHash, after.PasswordHash);
        Assert.Equal(UserRole.Admin, after.Role);
        Assert.Equal(1, await context.Users.CountAsync());
    }

    [Fact]
    public async Task Existing_same_name_non_admin_is_a_config_conflict()
    {
        await ClearUsersAsync();
        await using var context = fixture.CreateContext();
        context.Users.Add(new User("admin", "existing-hash", UserRole.User, Utc));
        await context.SaveChangesAsync();

        var seeder = CreateSeeder();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => seeder.SeedAsync(CancellationToken.None));

        var unchanged = await context.Users.SingleAsync(u => u.Username == "admin");
        Assert.Equal(UserRole.User, unchanged.Role);
        Assert.Equal("existing-hash", unchanged.PasswordHash);
    }

    [Fact]
    public async Task Concurrent_seed_attempts_converge_on_one_admin()
    {
        await ClearUsersAsync();
        var contextA = fixture.CreateContext();
        var contextB = fixture.CreateContext();
        var seederA = new AdminSeeder(contextA, new AdminSeedOptions("admin", "S3cret-Pass!"), new FixedTimeProvider(Utc), _hasher);
        var seederB = new AdminSeeder(contextB, new AdminSeedOptions("admin", "S3cret-Pass!"), new FixedTimeProvider(Utc), _hasher);

        await Task.WhenAll(
            seederA.SeedAsync(CancellationToken.None),
            seederB.SeedAsync(CancellationToken.None));

        await using var context = fixture.CreateContext();
        var admins = await context.Users.Where(u => u.Username == "admin").ToListAsync();
        Assert.Single(admins);
        Assert.Equal(UserRole.Admin, admins[0].Role);
    }

    [Fact]
    public async Task Missing_credentials_fail_fast_and_create_no_user()
    {
        await ClearUsersAsync();
        await using var context = fixture.CreateContext();
        var seeder = new AdminSeeder(context, new AdminSeedOptions("", ""), new FixedTimeProvider(Utc), _hasher);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => seeder.SeedAsync(CancellationToken.None));
        Assert.Equal(0, await context.Users.CountAsync());
    }

    private sealed class FixedTimeProvider(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }
}

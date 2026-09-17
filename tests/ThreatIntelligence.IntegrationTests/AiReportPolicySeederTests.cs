using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Infrastructure.AI;
using ThreatIntelligence.Application.AI;
using ThreatIntelligence.Infrastructure.Persistence;
using ThreatIntelligence.Infrastructure.Persistence.Seeding;

namespace ThreatIntelligence.IntegrationTests;

[Collection("sqlserver")]
public sealed class AiReportPolicySeederTests(SqlServerFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await fixture.ResetDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Seeds_version_one_from_the_default_resource_when_no_policy_exists()
    {
        await using var context = fixture.CreateContext();
        var seeder = new AiReportPolicySeeder(context, TimeProvider.System);

        await seeder.SeedAsync(CancellationToken.None);

        context.ChangeTracker.Clear();
        var version = await context.AiReportPolicyVersions.SingleAsync();
        var pointer = await context.AiReportPolicyActive.SingleAsync();
        Assert.Equal(1, version.Version);
        Assert.Equal(AiReportPolicySeeder.ReadDefaultReportText(), version.Text);
        Assert.Null(version.AuthorUserId);
        Assert.Equal(1, pointer.ActiveVersion);
    }

    [Fact]
    public async Task Repeated_startup_preserves_a_published_admin_version()
    {
        await using var context = fixture.CreateContext();
        var now = DateTime.UtcNow;
        context.AiReportPolicyVersions.Add(new ThreatIntelligence.Domain.Entities.AiReportPolicyVersion(
            1, "Yayınlanmış yönetici sürümü", "admin", now));
        context.AiReportPolicyActive.Add(new ThreatIntelligence.Domain.Entities.AiReportPolicyActive(1, now));
        await context.SaveChangesAsync();
        var seeder = new AiReportPolicySeeder(context, TimeProvider.System);

        await seeder.SeedAsync(CancellationToken.None);
        await seeder.SeedAsync(CancellationToken.None);

        context.ChangeTracker.Clear();
        Assert.Equal(1, await context.AiReportPolicyVersions.CountAsync());
        Assert.Equal(
            "Yayınlanmış yönetici sürümü",
            (await context.AiReportPolicyVersions.SingleAsync()).Text);
        Assert.Equal(1, (await context.AiReportPolicyActive.SingleAsync()).ActiveVersion);
    }
}

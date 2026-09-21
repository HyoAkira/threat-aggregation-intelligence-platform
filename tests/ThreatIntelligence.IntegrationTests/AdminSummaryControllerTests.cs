using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.API.Controllers;
using ThreatIntelligence.Application.Admin;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Admin;

namespace ThreatIntelligence.IntegrationTests;

[Collection("sqlserver")]
public sealed class AdminSummaryControllerTests(SqlServerFixture fixture) : IAsyncLifetime
{
    public async Task InitializeAsync() => await fixture.ResetDataAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Summary_counts_cross_provider_and_port_variants_once()
    {
        await using var context = fixture.CreateContext();
        // Same normalized IPv4 from two providers and two ports must count once.
        await IocQueryTestData.InsertIocAsync(context, 1, "sum-ip-1", "1.2.3.4", IocType.Ipv4, port: 80);
        await IocQueryTestData.InsertIocAsync(context, 2, "sum-ip-2", "1.2.3.4", IocType.Ipv4, port: 443);
        await IocQueryTestData.InsertIocAsync(context, 1, "sum-ipv6", "2001:db8::1", IocType.Ipv6);
        await IocQueryTestData.InsertIocAsync(context, 1, "sum-domain", "evil.example", IocType.Domain);
        await IocQueryTestData.InsertIocAsync(context, 2, "sum-url", "https://evil.example/x", IocType.Url);

        var controller = new AdminSummaryController(new AdminSummaryReader(context));
        var ok = Assert.IsType<OkObjectResult>((await controller.Summary(CancellationToken.None)).Result);
        var summary = Assert.IsType<AdminSummary>(ok.Value);

        Assert.Equal(4, summary.Total);
        Assert.Equal(1, summary.Ipv4);
        Assert.Equal(1, summary.Ipv6);
        Assert.Equal(1, summary.Domain);
        Assert.Equal(1, summary.Url);
    }

    [Fact]
    public async Task Summary_is_zero_when_empty()
    {
        await using var context = fixture.CreateContext();
        var controller = new AdminSummaryController(new AdminSummaryReader(context));

        var ok = Assert.IsType<OkObjectResult>((await controller.Summary(CancellationToken.None)).Result);
        var summary = Assert.IsType<AdminSummary>(ok.Value);
        Assert.Equal(0, summary.Total);
    }
}

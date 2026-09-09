using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Hashing;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.IntegrationTests;

public static class TestData
{
    public static readonly DateTime Utc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static byte[] HashOf(string value) => new Sha256ValueHasher().Hash(value);

    public static async Task InsertIocRawAsync(
        ThreatIntelligenceDbContext context,
        int providerId,
        string recordId,
        string iocValue,
        byte[] hash,
        byte iocType,
        int? port = null,
        byte? confidence = null,
        string? phishTankDetails = null)
    {
        await context.Database.ExecuteSqlAsync($"""
            INSERT INTO iocs (providerId, providerRecordId, iocValue, iocValueHash, iocType, port, firstSeenUtc, createdAt, updatedAt, threatFoxConfidenceLevel, phishTankDetails)
            VALUES ({providerId}, {recordId}, {iocValue}, {hash}, {iocType}, {port}, SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME(), {confidence}, {phishTankDetails})
            """);
    }
}

[Collection("sqlserver")]
public class ConstraintTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task Duplicate_null_port_endpoint_rows_are_rejected()
    {
        await using var context = fixture.CreateContext();
        await TestData.InsertIocRawAsync(context, 1, "a-1", "1.2.3.4", TestData.HashOf("1.2.3.4"), (byte)IocType.Ipv4);
        await Assert.ThrowsAsync<SqlException>(() =>
            TestData.InsertIocRawAsync(context, 1, "a-2", "1.2.3.4", TestData.HashOf("1.2.3.4"), (byte)IocType.Ipv4));
    }

    [Fact]
    public async Task Duplicate_equal_port_endpoint_rows_are_rejected()
    {
        await using var context = fixture.CreateContext();
        await TestData.InsertIocRawAsync(context, 1, "b-1", "5.6.7.8", TestData.HashOf("5.6.7.8"), (byte)IocType.Ipv4, port: 80);
        await Assert.ThrowsAsync<SqlException>(() =>
            TestData.InsertIocRawAsync(context, 1, "b-2", "5.6.7.8", TestData.HashOf("5.6.7.8"), (byte)IocType.Ipv4, port: 80));
    }

    [Fact]
    public async Task Different_port_endpoint_rows_are_allowed()
    {
        await using var context = fixture.CreateContext();
        var hash = TestData.HashOf("9.9.9.9");
        await TestData.InsertIocRawAsync(context, 1, "c-1", "9.9.9.9", hash, (byte)IocType.Ipv4, port: 80);
        await TestData.InsertIocRawAsync(context, 1, "c-2", "9.9.9.9", hash, (byte)IocType.Ipv4, port: 443);

        var count = await context.Iocs.CountAsync(i => i.IocValue == "9.9.9.9");
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Duplicate_provider_record_id_is_rejected()
    {
        await using var context = fixture.CreateContext();
        await TestData.InsertIocRawAsync(context, 1, "dup-1", "10.0.0.1", TestData.HashOf("10.0.0.1"), (byte)IocType.Ipv4);
        await Assert.ThrowsAsync<SqlException>(() =>
            TestData.InsertIocRawAsync(context, 1, "dup-1", "10.0.0.2", TestData.HashOf("10.0.0.2"), (byte)IocType.Ipv4));
    }

    [Fact]
    public async Task Same_normalized_ioc_from_different_providers_is_allowed()
    {
        await using var context = fixture.CreateContext();
        var hash = TestData.HashOf("10.5.5.5");
        await TestData.InsertIocRawAsync(context, 1, "p1", "10.5.5.5", hash, (byte)IocType.Ipv4);
        await TestData.InsertIocRawAsync(context, 2, "p2", "10.5.5.5", hash, (byte)IocType.Ipv4);

        var count = await context.Iocs.CountAsync(i => i.IocValue == "10.5.5.5");
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Duplicate_fetchHistory_jobId_is_rejected()
    {
        await using var context = fixture.CreateContext();
        var jobId = Guid.NewGuid();
        context.FetchHistory.Add(new FetchHistory(
            jobId, 1, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Running, TestData.Utc));
        await context.SaveChangesAsync();

        context.FetchHistory.Add(new FetchHistory(
            jobId, 1, FetchTrigger.Scheduled, FetchMode.Recent, FetchStatus.Running, TestData.Utc.AddMinutes(1)));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Duplicate_username_is_rejected()
    {
        await using var context = fixture.CreateContext();
        context.Users.Add(new User("admin", "hash", UserRole.Admin, TestData.Utc));
        await context.SaveChangesAsync();

        context.Users.Add(new User("admin", "other", UserRole.User, TestData.Utc));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Duplicate_settings_key_is_rejected()
    {
        await using var context = fixture.CreateContext();
        context.Settings.Add(new Settings("Logging:MinimumLevel", "Information", TestData.Utc));
        await context.SaveChangesAsync();

        context.Settings.Add(new Settings("Logging:MinimumLevel", "Debug", TestData.Utc));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Duplicate_tag_name_is_rejected()
    {
        await using var context = fixture.CreateContext();
        context.Tags.Add(new Tag("botnet-dup"));
        await context.SaveChangesAsync();

        context.Tags.Add(new Tag("botnet-dup"));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public async Task Out_of_range_port_is_rejected_by_the_database(int port)
    {
        await using var context = fixture.CreateContext();
        await Assert.ThrowsAsync<SqlException>(() =>
            TestData.InsertIocRawAsync(context, 1, "port-" + port, "1.1.1.1", TestData.HashOf("1.1.1.1"), (byte)IocType.Ipv4, port: port));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(65535)]
    public async Task Boundary_ports_are_accepted_by_the_database(int port)
    {
        await using var context = fixture.CreateContext();
        await TestData.InsertIocRawAsync(context, 1, "bound-" + port, "2.2.2.2", TestData.HashOf("2.2.2.2"), (byte)IocType.Ipv4, port: port);
        Assert.Equal(1, await context.Iocs.CountAsync(i => i.Port == port));
    }

    [Fact]
    public async Task Out_of_range_confidence_is_rejected_by_the_database()
    {
        await using var context = fixture.CreateContext();
        await Assert.ThrowsAsync<SqlException>(() =>
            TestData.InsertIocRawAsync(context, 1, "conf", "3.3.3.3", TestData.HashOf("3.3.3.3"), (byte)IocType.Ipv4, confidence: 101));
    }

[Theory]
    [InlineData((byte)0)]
    [InlineData((byte)100)]
    public async Task Boundary_confidences_are_accepted_by_the_database(byte confidence)
    {
        await fixture.ResetDataAsync();
        await using var context = fixture.CreateContext();
        var value = "4.4.4." + confidence;
        await TestData.InsertIocRawAsync(context, 1, "conf-" + confidence, value, TestData.HashOf(value), (byte)IocType.Ipv4, confidence: confidence);
        Assert.Equal(1, await context.Iocs.CountAsync(i => i.ThreatFoxConfidenceLevel == confidence));
    }

    [Fact]
    public async Task Malformed_json_details_are_rejected_by_the_database()
    {
        await using var context = fixture.CreateContext();
        await Assert.ThrowsAsync<SqlException>(() =>
            TestData.InsertIocRawAsync(context, 1, "json", "5.5.5.5", TestData.HashOf("5.5.5.5"), (byte)IocType.Ipv4, phishTankDetails: "not-json"));
    }

    [Fact]
    public async Task Valid_json_details_are_accepted_by_the_database()
    {
        await using var context = fixture.CreateContext();
        await TestData.InsertIocRawAsync(context, 1, "json-ok", "6.6.6.6", TestData.HashOf("6.6.6.6"), (byte)IocType.Ipv4, phishTankDetails: """[{"ipAddress":"1.2.3.4"}]""");
        Assert.Equal(1, await context.Iocs.CountAsync(i => i.PhishTankDetails != null));
    }
}
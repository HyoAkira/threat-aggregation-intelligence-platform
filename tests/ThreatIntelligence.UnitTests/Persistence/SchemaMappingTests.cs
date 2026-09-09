using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Infrastructure.Persistence;

namespace ThreatIntelligence.UnitTests.Persistence;

public class SchemaMappingTests
{
    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<ThreatIntelligenceDbContext>()
            .UseSqlServer("Server=localhost;Database=test;User Id=sa;Password=not-real;TrustServerCertificate=True")
            .Options;

        using var context = new ThreatIntelligenceDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    private static IEntityType Entity<T>() => BuildModel().GetEntityTypes()
        .Single(e => e.ClrType == typeof(T));

    private static IProperty Prop<T>(string property) =>
        Entity<T>().GetProperties().Single(p => p.Name == property);

    [Theory]
    [InlineData(typeof(Provider), "providers")]
    [InlineData(typeof(IoC), "iocs")]
    [InlineData(typeof(Tag), "tags")]
    [InlineData(typeof(IoCTag), "iocTags")]
    [InlineData(typeof(FetchHistory), "fetchHistory")]
    [InlineData(typeof(User), "users")]
    [InlineData(typeof(Settings), "settings")]
    public void Tables_use_plural_lower_camel_names(Type clrType, string expectedTable)
    {
        var entity = BuildModel().GetEntityTypes().Single(e => e.ClrType == clrType);
        Assert.Equal(expectedTable, entity.GetTableName());
    }

    [Theory]
    [InlineData(typeof(IoC), "IocValueHash", "binary(32)")]
    [InlineData(typeof(IoC), "PhishTankDetails", "nvarchar(max)")]
    [InlineData(typeof(FetchHistory), "ErrorDetail", "nvarchar(max)")]
    [InlineData(typeof(Settings), "Value", "nvarchar(max)")]
    [InlineData(typeof(Provider), "LastFetchAt", "datetime2")]
    [InlineData(typeof(IoC), "CreatedAt", "datetime2")]
    [InlineData(typeof(FetchHistory), "JobId", "uniqueidentifier")]
    public void Column_types_match_the_contract(Type clrType, string property, string expectedType)
    {
        var entity = BuildModel().GetEntityTypes().Single(e => e.ClrType == clrType);
        var prop = entity.GetProperties().Single(p => p.Name == property);
        Assert.Equal(expectedType, prop.GetColumnType());
    }

    [Theory]
    [InlineData(typeof(IoC), "IocValueHash", "iocValueHash")]
    [InlineData(typeof(IoC), "ProviderRecordId", "providerRecordId")]
    [InlineData(typeof(IoC), "PhishTankDetails", "phishTankDetails")]
    [InlineData(typeof(FetchHistory), "ErrorDetail", "errorDetail")]
    [InlineData(typeof(IoC), "CreatedAt", "createdAt")]
    [InlineData(typeof(FetchHistory), "JobId", "jobId")]
    [InlineData(typeof(Provider), "BaseUrl", "baseUrl")]
    public void Column_names_are_lower_camel_case(Type clrType, string property, string expectedColumn)
    {
        var entity = BuildModel().GetEntityTypes().Single(e => e.ClrType == clrType);
        var prop = entity.GetProperties().Single(p => p.Name == property);
        Assert.Equal(expectedColumn, prop.GetColumnName());
    }

    [Theory]
    [InlineData(typeof(Provider), "Name", 255)]
    [InlineData(typeof(Provider), "BaseUrl", 255)]
    [InlineData(typeof(IoC), "ProviderRecordId", 100)]
    [InlineData(typeof(IoC), "IocValue", 2048)]
    [InlineData(typeof(IoC), "ProviderDetailsLink", 2048)]
    [InlineData(typeof(IoC), "ThreatType", 100)]
    [InlineData(typeof(IoC), "Malware", 200)]
    [InlineData(typeof(IoC), "MalwareAlias", 500)]
    [InlineData(typeof(IoC), "MalwarePrintable", 200)]
    [InlineData(typeof(IoC), "Reporter", 200)]
    [InlineData(typeof(IoC), "Target", 500)]
    [InlineData(typeof(IoC), "UrlHost", 253)]
    [InlineData(typeof(IoC), "ParentDomain", 253)]
    [InlineData(typeof(Tag), "Name", 100)]
    [InlineData(typeof(User), "Username", 50)]
    [InlineData(typeof(User), "PasswordHash", 256)]
    [InlineData(typeof(Settings), "Key", 100)]
    public void Column_lengths_match_the_contract(Type clrType, string property, int expectedLength)
    {
        var entity = BuildModel().GetEntityTypes().Single(e => e.ClrType == clrType);
        var prop = entity.GetProperties().Single(p => p.Name == property);
        Assert.Equal(expectedLength, prop.GetMaxLength());
    }

    [Theory]
    [InlineData(typeof(IoC), "ProviderRecordId", false)]
    [InlineData(typeof(IoC), "IocValue", false)]
    [InlineData(typeof(IoC), "IocValueHash", false)]
    [InlineData(typeof(IoC), "ProviderDetailsLink", true)]
    [InlineData(typeof(IoC), "Port", true)]
    [InlineData(typeof(IoC), "ThreatFoxConfidenceLevel", true)]
    [InlineData(typeof(IoC), "UrlHost", true)]
    [InlineData(typeof(IoC), "ParentDomain", true)]
    [InlineData(typeof(Provider), "LastFetchStatus", true)]
    [InlineData(typeof(Provider), "NextFetchTime", true)]
    [InlineData(typeof(FetchHistory), "FinishedAt", true)]
    [InlineData(typeof(FetchHistory), "ErrorDetail", true)]
    [InlineData(typeof(FetchHistory), "RecordsAdded", false)]
    public void Column_nullability_matches_the_contract(Type clrType, string property, bool expectedNullable)
    {
        var entity = BuildModel().GetEntityTypes().Single(e => e.ClrType == clrType);
        var prop = entity.GetProperties().Single(p => p.Name == property);
        Assert.Equal(expectedNullable, prop.IsNullable);
    }

    [Theory]
    [InlineData(typeof(IoC), "IocType", "tinyint")]
    [InlineData(typeof(Provider), "LastFetchStatus", "tinyint")]
    [InlineData(typeof(FetchHistory), "Trigger", "tinyint")]
    [InlineData(typeof(FetchHistory), "Mode", "tinyint")]
    [InlineData(typeof(FetchHistory), "Status", "tinyint")]
    [InlineData(typeof(User), "Role", "tinyint")]
    public void Enums_persist_as_tinyint_ordinals(Type clrType, string property, string expectedType)
    {
        var entity = BuildModel().GetEntityTypes().Single(e => e.ClrType == clrType);
        var prop = entity.GetProperties().Single(p => p.Name == property);
        Assert.Equal(expectedType, prop.GetColumnType());
    }

    [Fact]
    public void IoC_has_provider_foreign_key()
    {
        var ioc = Entity<IoC>();
        var fk = Assert.Single(ioc.GetForeignKeys());
        Assert.Equal(typeof(Provider), fk.PrincipalEntityType.ClrType);
        Assert.Equal("ProviderId", Assert.Single(fk.Properties).Name);
    }

    [Fact]
    public void FetchHistory_has_provider_foreign_key()
    {
        var history = Entity<FetchHistory>();
        var fk = Assert.Single(history.GetForeignKeys());
        Assert.Equal(typeof(Provider), fk.PrincipalEntityType.ClrType);
        Assert.Equal("ProviderId", Assert.Single(fk.Properties).Name);
    }

    [Fact]
    public void IoCTag_has_composite_primary_key()
    {
        var entity = Entity<IoCTag>();
        var key = Assert.Single(entity.GetKeys());
        Assert.Equal(new[] { "IocId", "TagId" }, key.Properties.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void IoCTag_has_ignored_id_for_join_table()
    {
        var entity = Entity<IoCTag>();
        Assert.DoesNotContain(entity.GetProperties(), p => p.Name == "Id");
    }

    [Fact]
    public void PortKey_computed_column_uses_coalesce_and_is_stored()
    {
        var prop = Prop<IoC>("PortKey");
        Assert.Equal("COALESCE([Port], 0)", prop.GetComputedColumnSql());
        Assert.Equal(true, prop.FindAnnotation(RelationalAnnotationNames.IsStored)?.Value);
        Assert.Equal("portKey", prop.GetColumnName());
    }

    [Fact]
    public void Endpoint_identity_unique_index_uses_portKey()
    {
        var index = Entity<IoC>().GetIndexes()
            .Single(i => i.GetDatabaseName() == "IX_iocs_EndpointIdentity");
        Assert.True(index.IsUnique);
        Assert.Equal(new[] { "ProviderId", "IocType", "IocValueHash", "PortKey" },
            index.Properties.Select(p => p.Name).ToArray());
    }

    [Theory]
    [InlineData("IX_iocs_ProviderRecordId", new[] { "ProviderId", "ProviderRecordId" })]
    [InlineData("IX_iocs_IocType_IocValueHash", new[] { "IocType", "IocValueHash" })]
    [InlineData("IX_iocs_IocType_UrlHost", new[] { "IocType", "UrlHost" })]
    [InlineData("IX_iocs_IocType_ParentDomain", new[] { "IocType", "ParentDomain" })]
    [InlineData("IX_iocs_LastSeenUtc", new[] { "LastSeenUtc" })]
    public void IoC_supporting_indexes_exist(string indexName, string[] columns)
    {
        var index = Entity<IoC>().GetIndexes().Single(i => i.GetDatabaseName() == indexName);
        Assert.Equal(columns, index.Properties.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void ProviderRecordId_index_is_unique()
    {
        Assert.True(Entity<IoC>().GetIndexes()
            .Single(i => i.GetDatabaseName() == "IX_iocs_ProviderRecordId").IsUnique);
    }

    [Theory]
    [InlineData("IX_fetchHistory_JobId", true)]
    [InlineData("IX_fetchHistory_ProviderId_StartedAt", false)]
    public void FetchHistory_indexes_exist(string indexName, bool unique)
    {
        var index = Entity<FetchHistory>().GetIndexes().Single(i => i.GetDatabaseName() == indexName);
        Assert.Equal(unique, index.IsUnique);
    }

    [Theory]
    [InlineData(typeof(User), "IX_users_Username")]
    [InlineData(typeof(Settings), "IX_settings_Key")]
    [InlineData(typeof(Tag), "IX_tags_Name")]
    public void Unique_single_column_indexes_exist(Type clrType, string indexName)
    {
        var index = BuildModel().GetEntityTypes().Single(e => e.ClrType == clrType)
            .GetIndexes().Single(i => i.GetDatabaseName() == indexName);
        Assert.True(index.IsUnique);
    }

    [Theory]
    [InlineData("CK_iocs_Port", "[Port] IS NULL OR ([Port] >= 1 AND [Port] <= 65535)")]
    [InlineData("CK_iocs_ThreatFoxConfidenceLevel",
        "[ThreatFoxConfidenceLevel] IS NULL OR ([ThreatFoxConfidenceLevel] >= 0 AND [ThreatFoxConfidenceLevel] <= 100)")]
    [InlineData("CK_iocs_PhishTankDetails", "[PhishTankDetails] IS NULL OR ISJSON([PhishTankDetails]) = 1")]
    public void IoC_check_constraints_exist(string name, string expectedSql)
    {
        var check = Entity<IoC>().GetCheckConstraints().Single(c => c.Name == name);
        Assert.Equal(expectedSql, check.Sql);
    }
}
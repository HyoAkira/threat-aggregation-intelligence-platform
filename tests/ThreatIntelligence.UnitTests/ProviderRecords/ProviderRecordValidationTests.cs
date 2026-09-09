using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.ProviderRecords;

public class ProviderRecordValidationTests
{
    [Fact]
    public void A_fully_valid_record_returns_a_normalized_provider_record()
    {
        var result = ProviderValidationFactory.CreateValidatorWithDefault()
            .Validate(ProviderValidationFactory.ValidInput(), recordIndex: 1);

        Assert.True(result.IsValid);
        Assert.Null(result.Failure);
        Assert.NotNull(result.Normalized);

        var record = result.Normalized;
        Assert.Equal(1, record.ProviderId);
        Assert.Equal("ioc-123", record.ProviderRecordId);
        Assert.Equal(IocType.Domain, record.IocIdentity.Type);
        Assert.Equal("api.example.co.uk", record.IocIdentity.IocValue);
        Assert.Equal("example.co.uk", record.IocIdentity.ParentDomain);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero), record.FirstSeenUtc);
        Assert.Equal(new DateTimeOffset(2026, 9, 7, 10, 30, 0, TimeSpan.Zero), record.LastSeenUtc);
        Assert.Equal("https://threatfox.example/ioc123", record.ProviderDetailsLink);
        Assert.Equal(new[] { "botnet" }, record.Tags);
        Assert.Null(record.Metadata);
        Assert.Equal("trickbot", record.Payload["malware"]);
    }

    [Fact]
    public void An_ip_based_record_is_normalized_without_url_or_parent_domain()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            RawIoc = "1.2.3.4:443",
            DeclaredType = IocType.Ipv4,
            ProviderDetailsLink = null,
            Tags = null
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.True(result.IsValid);
        var record = result.Normalized;
        Assert.Equal(IocType.Ipv4, record.IocIdentity.Type);
        Assert.Equal("1.2.3.4", record.IocIdentity.IocValue);
        Assert.Equal(443, record.IocIdentity.Port);
        Assert.Null(record.IocIdentity.UrlHost);
        Assert.Null(record.IocIdentity.ParentDomain);
    }

    [Fact]
    public void The_provided_record_index_is_reported_in_failures()
    {
        var input = ProviderValidationFactory.ValidInput() with { RawIoc = "  " };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 42);

        Assert.Equal(42, result.Failure.RecordIndex);
    }
}
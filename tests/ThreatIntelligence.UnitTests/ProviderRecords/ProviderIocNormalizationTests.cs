using ThreatIntelligence.Application.ProviderRecords;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.ProviderRecords;

public class ProviderIocNormalizationTests
{
    [Fact]
    public void Raw_ioc_is_normalized_when_declared_type_matches()
    {
        var input = ProviderValidationFactory.ValidInput();

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.True(result.IsValid);
        Assert.Equal(IocType.Domain, result.Normalized.IocIdentity.Type);
        Assert.Equal("api.example.co.uk", result.Normalized.IocIdentity.IocValue);
        Assert.Equal("example.co.uk", result.Normalized.IocIdentity.ParentDomain);
    }

    [Fact]
    public void Declared_domain_but_url_raw_ioc_returns_stable_type_mismatch()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            RawIoc = "http://ctx.example/x",
            DeclaredType = IocType.Domain
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.False(result.IsValid);
        Assert.Equal(ProviderRecordFailureCode.TypeMismatch, result.Failure.Code);
        Assert.Equal("ioc", result.Failure.Field);
    }

    [Fact]
    public void Declared_url_but_ipv4_raw_ioc_returns_type_mismatch()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            RawIoc = "1.2.3.4",
            DeclaredType = IocType.Url
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.Equal(ProviderRecordFailureCode.TypeMismatch, result.Failure.Code);
    }

    [Fact]
    public void Declared_ipv4_with_bare_ipv4_raw_ioc_passes()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            RawIoc = "1.2.3.4",
            DeclaredType = IocType.Ipv4
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.True(result.IsValid);
        Assert.Equal(IocType.Ipv4, result.Normalized.IocIdentity.Type);
        Assert.Equal("1.2.3.4", result.Normalized.IocIdentity.IocValue);
    }

    [Fact]
    public void Undeclared_ipv6_raw_ioc_auto_classifies()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            RawIoc = "2001:0DB8::1",
            DeclaredType = null
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.True(result.IsValid);
        Assert.Equal(IocType.Ipv6, result.Normalized.IocIdentity.Type);
        Assert.Equal("2001:db8::1", result.Normalized.IocIdentity.IocValue);
    }

    [Fact]
    public void Unclassifiable_raw_ioc_returns_invalid_ioc()
    {
        var input = ProviderValidationFactory.ValidInput() with
        {
            RawIoc = "foo_bar.example",
            DeclaredType = null
        };

        var result = ProviderValidationFactory.CreateValidatorWithDefault().Validate(input, recordIndex: 1);

        Assert.False(result.IsValid);
        Assert.Equal(ProviderRecordFailureCode.InvalidIoc, result.Failure.Code);
        Assert.Equal("ioc", result.Failure.Field);
    }
}
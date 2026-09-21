namespace ThreatIntelligence.Application.ProviderRecords;

public interface IProviderValidator
{
    int ProviderId { get; }

    ProviderRejection? Validate(ProviderRecordInput input);
}
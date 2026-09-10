using ThreatIntelligence.Application.ProviderRecords;

namespace ThreatIntelligence.Infrastructure.PhishTank;

public sealed class PhishTankProviderValidator : IProviderValidator
{
    public const int PhishTankProviderId = 2;

    public int ProviderId => PhishTankProviderId;

    public ProviderRejection? Validate(ProviderRecordInput input)
    {
        if (input.ProviderId != PhishTankProviderId)
        {
            return new ProviderRejection(
                "provider",
                "PhishTank validator only accepts records from the PhishTank provider.");
        }

        return null;
    }
}

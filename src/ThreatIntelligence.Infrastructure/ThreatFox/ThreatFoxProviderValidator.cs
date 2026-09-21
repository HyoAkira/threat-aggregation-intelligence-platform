using ThreatIntelligence.Application.ProviderRecords;

namespace ThreatIntelligence.Infrastructure.ThreatFox;

/// <summary>
/// ThreatFox-specific provider validator on the E03 extension point. Fails closed when the record
/// is not a ThreatFox record and enforces ThreatFox payload rules (confidence 0-100) after E03
/// common validation succeeds.
/// </summary>
public sealed class ThreatFoxProviderValidator : IProviderValidator
{
    public const int ThreatFoxProviderId = 1;

    public int ProviderId => ThreatFoxProviderId;

    public ProviderRejection? Validate(ProviderRecordInput input)
    {
        if (input.ProviderId != ThreatFoxProviderId)
        {
            return new ProviderRejection(
                "provider",
                "ThreatFox validator only accepts records from the ThreatFox provider.");
        }

        if (input.Metadata is not null
            && input.Metadata.TryGetValue(ThreatFoxRecordMapper.ConfidenceLevelKey, out var confidenceValue)
            && confidenceValue is not null
            && confidenceValue is int confidenceLevel
            && confidenceLevel is < 0 or > 100)
        {
            return new ProviderRejection(
                ThreatFoxRecordMapper.ConfidenceLevelKey,
                "ThreatFox confidence level must be between 0 and 100.");
        }

        return null;
    }
}
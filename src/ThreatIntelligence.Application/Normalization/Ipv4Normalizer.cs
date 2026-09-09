using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Application.Normalization;

public interface IIpv4Normalizer
{
    NormalizationOutput Normalize(string input);
}

public class Ipv4Normalizer : IIpv4Normalizer
{
    public NormalizationOutput Normalize(string input)
    {
        var trimmed = input.Trim();
        if (trimmed.Length == 0)
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.InvalidInput,
                ErrorMessages.InvalidInput);
        }

        var (addressPart, portText) = SplitPort(trimmed);

        if (!Ipv4Parser.TryCanonicalize(addressPart, out var canonical))
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.InvalidInput,
                ErrorMessages.InvalidInput);
        }

        if (portText is null)
        {
            return NormalizationOutput.Success(
                new NormalizationResult(IocType.Ipv4, canonical, null, null, null));
        }

        if (!PortParser.TryParse(portText, out var port))
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.InvalidIpv4Port,
                ErrorMessages.InvalidIpv4Port);
        }

        return NormalizationOutput.Success(
            new NormalizationResult(IocType.Ipv4, canonical, port, null, null));
    }

    private static (string address, string? port) SplitPort(string input)
    {
        var lastColon = input.LastIndexOf(':');
        if (lastColon < 0)
        {
            return (input, null);
        }

        return (input[..lastColon], input[(lastColon + 1)..]);
    }
}
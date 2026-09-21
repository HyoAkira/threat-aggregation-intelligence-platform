using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Application.Normalization;

public interface IIpv6Normalizer
{
    NormalizationOutput Normalize(string input);
}

public class Ipv6Normalizer : IIpv6Normalizer
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

        if (trimmed.StartsWith('['))
        {
            return NormalizeBracketed(trimmed);
        }

        if (trimmed.Contains('[') || trimmed.Contains(']'))
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.UnmatchedBrackets,
                ErrorMessages.UnmatchedBrackets);
        }

        return NormalizeBare(trimmed);
    }

    private NormalizationOutput NormalizeBracketed(string input)
    {
        var closeIndex = input.IndexOf(']');
        if (closeIndex < 0)
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.UnmatchedBrackets,
                ErrorMessages.UnmatchedBrackets);
        }

        var addressText = input[1..closeIndex];
        var rest = input[(closeIndex + 1)..];

        string? portText = null;
        if (rest.Length > 0)
        {
            if (!rest.StartsWith(':'))
            {
                return NormalizationOutput.Failure(
                    NormalizationErrorCode.UnmatchedBrackets,
                    ErrorMessages.UnmatchedBrackets);
            }

            portText = rest[1..];
        }

        if (!TryParseAddress(addressText, out var canonical))
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.InvalidIpv6,
                ErrorMessages.InvalidIpv6);
        }

        if (portText is null)
        {
            return NormalizationOutput.Success(
                new NormalizationResult(IocType.Ipv6, canonical, null, null, null));
        }

        if (!PortParser.TryParse(portText, out var port))
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.InvalidIpv6Port,
                ErrorMessages.InvalidIpv6Port);
        }

        return NormalizationOutput.Success(
            new NormalizationResult(IocType.Ipv6, canonical, port, null, null));
    }

    private NormalizationOutput NormalizeBare(string input)
    {
        if (!TryParseAddress(input, out var canonical))
        {
            return NormalizationOutput.Failure(
                NormalizationErrorCode.InvalidIpv6,
                ErrorMessages.InvalidIpv6);
        }

        return NormalizationOutput.Success(
            new NormalizationResult(IocType.Ipv6, canonical, null, null, null));
    }

    private static bool TryParseAddress(string text, out string canonical)
        => Ipv6Parser.TryCanonicalize(text, out canonical);
}
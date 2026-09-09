using System.Net;
using System.Net.Sockets;

namespace ThreatIntelligence.Application.Normalization;

internal static class Ipv4Parser
{
    public static bool TryCanonicalize(string address, out string canonical)
    {
        canonical = string.Empty;

        if (!ValidateOctets(address))
        {
            return false;
        }

        if (!IPAddress.TryParse(address, out var parsed) ||
            parsed.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        canonical = parsed.ToString();
        return true;
    }

    public static bool IsIpv4Shaped(string host)
    {
        if (host.Length == 0 || !host.Contains('.'))
        {
            return false;
        }

        foreach (var c in host)
        {
            if (!char.IsAsciiDigit(c) && c != '.')
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateOctets(string address)
    {
        var span = address.AsSpan();
        var dotCount = 0;
        var segmentStart = 0;

        for (var i = 0; i <= span.Length; i++)
        {
            if (i == span.Length || span[i] == '.')
            {
                if (dotCount >= 3 && i < span.Length)
                {
                    return false;
                }

                var segment = span[segmentStart..i];
                if (segment.Length == 0 || segment.Length > 3)
                {
                    return false;
                }

                if (segment.Length > 1 && segment[0] == '0')
                {
                    return false;
                }

                for (var j = 0; j < segment.Length; j++)
                {
                    if (!char.IsAsciiDigit(segment[j]))
                    {
                        return false;
                    }
                }

                if (!int.TryParse(segment, out var value) || value > 255)
                {
                    return false;
                }

                segmentStart = i + 1;
                dotCount++;
            }
        }

        return dotCount == 4;
    }
}
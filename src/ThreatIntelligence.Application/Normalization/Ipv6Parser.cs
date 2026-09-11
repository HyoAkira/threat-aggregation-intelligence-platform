using System.Net;
using System.Net.Sockets;

namespace ThreatIntelligence.Application.Normalization;

internal static class Ipv6Parser
{
    public static bool TryCanonicalize(string address, out string canonical)
    {
        canonical = string.Empty;

        if (address.Length == 0 || address.Contains('%'))
        {
            return false;
        }

        if (!IPAddress.TryParse(address, out var parsed) ||
            parsed.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return false;
        }

        canonical = parsed.ToString().ToLowerInvariant();
        return true;
    }
}
namespace ThreatIntelligence.Application.Normalization;

public static class PortParser
{
    public static bool TryParse(string text, out int port)
    {
        port = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var c in text)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return int.TryParse(text, out port) && port is >= 1 and <= 65535;
    }
}
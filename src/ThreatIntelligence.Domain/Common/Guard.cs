namespace ThreatIntelligence.Domain.Common;

public static class Guard
{
    public static string Required(string? value, int maxLength, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} must not be blank.", paramName);
        }

        if (value.Length > maxLength)
        {
            throw new ArgumentException($"{paramName} must not exceed {maxLength} characters.", paramName);
        }

        return value;
    }

    public static string Required(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} must not be blank.", paramName);
        }

        return value;
    }

    public static DateTime Utc(DateTime value, string paramName)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException($"{paramName} must be a UTC timestamp.", paramName);
        }

        return value;
    }

    public static DateTime? UtcOrNull(DateTime? value, string paramName)
    {
        if (value is { Kind: not DateTimeKind.Utc })
        {
            throw new ArgumentException($"{paramName} must be a UTC timestamp.", paramName);
        }

        return value;
    }

    public static int Positive(int value, string paramName)
    {
        if (value <= 0)
        {
            throw new ArgumentException($"{paramName} must be a positive value.", paramName);
        }

        return value;
    }

    public static int NonNegative(int value, string paramName)
    {
        if (value < 0)
        {
            throw new ArgumentException($"{paramName} must be a non-negative value.", paramName);
        }

        return value;
    }

    public static byte[] ExactBytes(byte[] value, int length, string paramName)
    {
        if (value.Length != length)
        {
            throw new ArgumentException($"{paramName} must be exactly {length} bytes.", paramName);
        }

        return value;
    }

    public static TEnum Defined<TEnum>(TEnum value, string paramName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(typeof(TEnum), value))
        {
            throw new ArgumentException($"{paramName} has an undefined value '{value}'.", paramName);
        }

        return value;
    }
}

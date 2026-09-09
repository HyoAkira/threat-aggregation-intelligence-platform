using ThreatIntelligence.Domain.Common;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Domain.Entities;

public class IoC
{
    private IoC()
    {
    }

    public IoC(
        int providerId,
        string providerRecordId,
        string iocValue,
        byte[] iocValueHash,
        IocType iocType,
        DateTime firstSeenUtc,
        DateTime createdAt,
        DateTime updatedAt,
        string? providerDetailsLink = null,
        int? port = null,
        string? threatType = null,
        string? malware = null,
        string? malwareAlias = null,
        string? malwarePrintable = null,
        DateTime? lastSeenUtc = null,
        bool? isCompromised = null,
        string? reporter = null,
        bool? anonymous = null,
        bool? isVerified = null,
        string? target = null,
        string? phishTankDetails = null,
        bool? phishTankOnline = null,
        string? urlHost = null,
        string? parentDomain = null,
        byte? threatFoxConfidenceLevel = null)
    {
        ProviderId = Guard.Positive(providerId, nameof(providerId));
        ProviderRecordId = Guard.Required(providerRecordId, 100, nameof(providerRecordId));
        IocValue = Guard.Required(iocValue, 2048, nameof(iocValue));
        IocValueHash = Guard.ExactBytes(iocValueHash, 32, nameof(iocValueHash));
        IocType = Guard.Defined(iocType, nameof(iocType));
        FirstSeenUtc = Guard.Utc(firstSeenUtc, nameof(firstSeenUtc));
        LastSeenUtc = Guard.UtcOrNull(lastSeenUtc, nameof(lastSeenUtc));
        CreatedAt = Guard.Utc(createdAt, nameof(createdAt));
        UpdatedAt = Guard.Utc(updatedAt, nameof(updatedAt));

        Port = ValidatePort(port);
        ThreatFoxConfidenceLevel = ValidateConfidence(threatFoxConfidenceLevel);

        ProviderDetailsLink = providerDetailsLink;
        ThreatType = threatType;
        Malware = malware;
        MalwareAlias = malwareAlias;
        MalwarePrintable = malwarePrintable;
        IsCompromised = isCompromised;
        Reporter = reporter;
        Anonymous = anonymous;
        IsVerified = isVerified;
        Target = target;
        PhishTankDetails = phishTankDetails;
        PhishTankOnline = phishTankOnline;
        UrlHost = urlHost;
        ParentDomain = parentDomain;
    }

    private static int? ValidatePort(int? port)
    {
        if (port.HasValue && port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "Port must be between 1 and 65535, or null.");
        }

        return port;
    }

    private static byte? ValidateConfidence(byte? confidence)
    {
        if (confidence.HasValue && confidence.Value > 100)
        {
            throw new ArgumentOutOfRangeException(
                "threatFoxConfidenceLevel", confidence, "Confidence must be between 0 and 100, or null.");
        }

        return confidence;
    }

    public int Id { get; private set; }
    public int ProviderId { get; private set; }
    public string ProviderRecordId { get; private set; } = string.Empty;
    public string? ProviderDetailsLink { get; private set; }
    public string IocValue { get; private set; } = string.Empty;
    public byte[] IocValueHash { get; private set; } = [];
    public IocType IocType { get; private set; }
    public int? Port { get; private set; }
    public string? ThreatType { get; private set; }
    public string? Malware { get; private set; }
    public string? MalwareAlias { get; private set; }
    public string? MalwarePrintable { get; private set; }
    public DateTime FirstSeenUtc { get; private set; }
    public DateTime? LastSeenUtc { get; private set; }
    public bool? IsCompromised { get; private set; }
    public string? Reporter { get; private set; }
    public bool? Anonymous { get; private set; }
    public bool? IsVerified { get; private set; }
    public string? Target { get; private set; }
    public string? PhishTankDetails { get; private set; }
    public bool? PhishTankOnline { get; private set; }
    public string? UrlHost { get; private set; }
    public string? ParentDomain { get; private set; }
    public byte? ThreatFoxConfidenceLevel { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
}

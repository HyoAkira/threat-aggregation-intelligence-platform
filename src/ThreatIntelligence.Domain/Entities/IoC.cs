using ThreatIntelligence.Domain.Common;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Domain.Entities;

/// <summary>
/// Mutable provider fields applied to an existing IOC row by an upsert. Immutable fields
/// (<see cref="IoC.CreatedAt"/> and stored <see cref="IoC.FirstSeenUtc"/>) are intentionally absent.
/// </summary>
public sealed record IocUpsertFields(
    string ProviderRecordId,
    string IocValue,
    byte[] IocValueHash,
    IocType IocType,
    int? Port,
    string? ProviderDetailsLink,
    DateTime UpdatedAtUtc,
    DateTime FirstSeenUtc,
    DateTime? LastSeenUtc,
    IReadOnlyList<string> Tags,
    string? ThreatType,
    string? Malware,
    string? MalwareAlias,
    string? MalwarePrintable,
    bool? IsCompromised,
    string? Reporter,
    bool? Anonymous,
    bool? IsVerified,
    string? Target,
    string? PhishTankDetails,
    bool? PhishTankOnline,
    string? UrlHost,
    string? ParentDomain,
    byte? ThreatFoxConfidenceLevel);

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

    /// <summary>
    /// Applies a provider upsert to an existing row. Immutable fields (<see cref="CreatedAt"/> and
    /// the stored <see cref="FirstSeenUtc"/>) are preserved; the provider's normalized identity,
    /// metadata, and timestamps are refreshed from the incoming fields.
    /// </summary>
    public void ApplyProviderUpsert(IocUpsertFields fields)
    {
        ProviderRecordId = Guard.Required(fields.ProviderRecordId, 100, nameof(fields.ProviderRecordId));
        IocValue = Guard.Required(fields.IocValue, 2048, nameof(fields.IocValue));
        IocValueHash = Guard.ExactBytes(fields.IocValueHash, 32, nameof(fields.IocValueHash));
        IocType = Guard.Defined(fields.IocType, nameof(fields.IocType));
        Port = ValidatePort(fields.Port);
        ThreatFoxConfidenceLevel = ValidateConfidence(fields.ThreatFoxConfidenceLevel);

        ProviderDetailsLink = fields.ProviderDetailsLink;
        LastSeenUtc = Guard.UtcOrNull(fields.LastSeenUtc, nameof(fields.LastSeenUtc));
        UpdatedAt = Guard.Utc(fields.UpdatedAtUtc, nameof(fields.UpdatedAtUtc));
        ThreatType = fields.ThreatType;
        Malware = fields.Malware;
        MalwareAlias = fields.MalwareAlias;
        MalwarePrintable = fields.MalwarePrintable;
        IsCompromised = fields.IsCompromised;
        Reporter = fields.Reporter;
        Anonymous = fields.Anonymous;
        IsVerified = fields.IsVerified;
        Target = fields.Target;
        PhishTankDetails = fields.PhishTankDetails;
        PhishTankOnline = fields.PhishTankOnline;
        UrlHost = fields.UrlHost;
        ParentDomain = fields.ParentDomain;
    }
}

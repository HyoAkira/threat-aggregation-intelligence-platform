using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Infrastructure.Services;

public sealed record NormalizedIoc(
    IocType IocType,
    string IocValue,
    byte[] IocValueHash,
    int? Port,
    string? UrlHost,
    string? ParentDomain);

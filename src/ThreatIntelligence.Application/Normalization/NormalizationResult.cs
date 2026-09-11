using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Application.Normalization;

public record NormalizationResult(
    IocType Type,
    string IocValue,
    int? Port,
    string? UrlHost,
    string? ParentDomain);

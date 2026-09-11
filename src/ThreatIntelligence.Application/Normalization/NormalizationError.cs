namespace ThreatIntelligence.Application.Normalization;

public record NormalizationError(
    NormalizationErrorCode Code,
    string Message);

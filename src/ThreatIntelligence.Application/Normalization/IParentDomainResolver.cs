namespace ThreatIntelligence.Application.Normalization;

public interface IParentDomainResolver
{
    string? Resolve(string host);
}
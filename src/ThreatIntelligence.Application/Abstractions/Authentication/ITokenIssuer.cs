using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Application.Abstractions.Authentication;

public interface ITokenIssuer
{
    IssuedToken Issue(User user);
}

public sealed record IssuedToken(string Value, DateTimeOffset ExpiresAt);

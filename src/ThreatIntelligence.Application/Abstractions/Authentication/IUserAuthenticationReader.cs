using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Application.Abstractions.Authentication;

public interface IUserAuthenticationReader
{
    Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken);
}

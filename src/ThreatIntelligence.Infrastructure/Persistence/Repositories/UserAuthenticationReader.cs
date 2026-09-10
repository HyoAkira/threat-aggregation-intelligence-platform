using Microsoft.EntityFrameworkCore;
using ThreatIntelligence.Application.Abstractions.Authentication;
using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.Infrastructure.Persistence.Repositories;

public sealed class UserAuthenticationReader(ThreatIntelligenceDbContext dbContext) : IUserAuthenticationReader
{
    public Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Username == username, cancellationToken);
}

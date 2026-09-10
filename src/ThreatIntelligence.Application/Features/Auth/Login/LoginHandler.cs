using MediatR;
using ThreatIntelligence.Application.Abstractions.Authentication;

namespace ThreatIntelligence.Application.Features.Auth.Login;

public sealed class LoginHandler(
    IUserAuthenticationReader users,
    IPasswordHasher passwords,
    ITokenIssuer tokens) : IRequestHandler<LoginCommand, LoginResult?>
{
    public async Task<LoginResult?> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await users.FindByUsernameAsync(request.Username, cancellationToken);
        var passwordMatches = passwords.VerifyPassword(request.Password, user?.PasswordHash);
        if (user is null || !passwordMatches)
        {
            return null;
        }

        var issued = tokens.Issue(user);
        return new LoginResult(issued.Value, user.Username, user.Role.ToString(), issued.ExpiresAt);
    }
}

using MediatR;

namespace ThreatIntelligence.Application.Features.Auth.Login;

public sealed record LoginCommand(string Username, string Password) : IRequest<LoginResult?>
{
    public override string ToString() => "LoginCommand { Username = [REDACTED], Password = [REDACTED] }";
}

public sealed record LoginResult(string Token, string Username, string Role, DateTimeOffset ExpiresAt)
{
    public override string ToString() =>
        $"LoginResult {{ Token = [REDACTED], Username = {Username}, Role = {Role}, ExpiresAt = {ExpiresAt:O} }}";
}

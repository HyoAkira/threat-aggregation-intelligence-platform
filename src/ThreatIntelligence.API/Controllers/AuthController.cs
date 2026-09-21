using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThreatIntelligence.Application.Features.Auth.Login;
using ThreatIntelligence.API.Errors;

namespace ThreatIntelligence.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new LoginCommand(request.Username, request.Password), cancellationToken);
        return result is null
            ? ApiProblemFactory.Result(
                HttpContext,
                StatusCodes.Status401Unauthorized,
                ApiProblemFactory.UnauthenticatedCode,
                "Unauthorized",
                "Authentication is required to access this resource.")
            : Ok(new LoginResponse(result.Token, result.Username, result.Role, result.ExpiresAt));
    }
}

public sealed record LoginRequest(string Username, string Password)
{
    public override string ToString() => "LoginRequest { Username = [REDACTED], Password = [REDACTED] }";
}

public sealed record LoginResponse(string Token, string Username, string Role, DateTimeOffset ExpiresAt)
{
    public override string ToString() =>
        $"LoginResponse {{ Token = [REDACTED], Username = {Username}, Role = {Role}, ExpiresAt = {ExpiresAt:O} }}";
}

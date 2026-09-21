using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using ThreatIntelligence.Application.Abstractions.Authentication;
using ThreatIntelligence.Domain.Entities;
using ThreatIntelligence.Infrastructure.Configuration;

namespace ThreatIntelligence.Infrastructure.Authentication;

public sealed class JwtTokenIssuer(JwtOptions options, TimeProvider timeProvider) : ITokenIssuer
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);
    private readonly SigningCredentials _credentials = new(
        CreateRsaKey(options.PrivateKey),
        SecurityAlgorithms.RsaSha256);

    private static RsaSecurityKey CreateRsaKey(string privateKey)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(privateKey.Replace("\\n", "\n"));
        return new RsaSecurityKey(rsa);
    }

    public IssuedToken Issue(User user)
    {
        var issuedAt = timeProvider.GetUtcNow();
        var expiresAt = issuedAt.Add(Lifetime);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            ]),
            SigningCredentials = _credentials
        };

        return new IssuedToken(new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}

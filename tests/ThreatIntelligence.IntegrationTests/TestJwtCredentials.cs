using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace ThreatIntelligence.IntegrationTests;

internal static class TestJwtCredentials
{
    public static readonly string PrivateKeyFile = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../secrets/jwt-private-key.pem"));
    public static string PrivateKey => File.ReadAllText(PrivateKeyFile);

    public static SigningCredentials Create(string algorithm = SecurityAlgorithms.RsaSha256)
    {
        if (algorithm == SecurityAlgorithms.RsaSha256)
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(PrivateKey);
            return new SigningCredentials(new RsaSecurityKey(rsa), algorithm);
        }

        return new SigningCredentials(
            new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes("invalid-test-secret-that-is-long-enough-for-hs384")),
            algorithm);
    }

}
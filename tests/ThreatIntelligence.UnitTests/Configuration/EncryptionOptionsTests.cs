using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using ThreatIntelligence.Infrastructure.Configuration;

namespace ThreatIntelligence.UnitTests.Configuration;

public sealed class EncryptionOptionsTests
{
    [Fact]
    public void Missing_root_key_disables_secret_management_without_throwing()
    {
        var options = EncryptionOptions.FromConfiguration(new ConfigurationBuilder().Build());

        Assert.False(options.IsAvailable);
    }

    [Fact]
    public void A_base64_32_byte_root_key_is_accepted()
    {
        var rootKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:RootKey"] = rootKey,
                ["Encryption:KeyVersion"] = "3"
            })
            .Build();

        var options = EncryptionOptions.FromConfiguration(configuration);

        Assert.True(options.IsAvailable);
        Assert.Equal(3, options.KeyVersion);
    }

    [Fact]
    public void Relative_root_key_file_is_resolved_from_dotenv_directory()
    {
        lock (typeof(EncryptionOptionsTests))
        {
            var originalDirectory = Directory.GetCurrentDirectory();
            var root = Directory.CreateTempSubdirectory("encryption-options-");
            var workingDirectory = Directory.CreateDirectory(Path.Combine(root.FullName, "service"));
            var keyDirectory = Directory.CreateDirectory(Path.Combine(root.FullName, "secrets"));
            var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            File.WriteAllText(Path.Combine(root.FullName, ".env"), string.Empty);
            File.WriteAllText(Path.Combine(keyDirectory.FullName, "root-key"), key);

            try
            {
                Directory.SetCurrentDirectory(workingDirectory.FullName);
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Encryption:RootKeyFile"] = "secrets/root-key",
                        ["Encryption:KeyVersion"] = "4"
                    })
                    .Build();

                var options = EncryptionOptions.FromConfiguration(configuration);

                Assert.True(options.IsAvailable);
                Assert.Equal(4, options.KeyVersion);
            }
            finally
            {
                Directory.SetCurrentDirectory(originalDirectory);
                root.Delete(recursive: true);
            }
        }
    }
}

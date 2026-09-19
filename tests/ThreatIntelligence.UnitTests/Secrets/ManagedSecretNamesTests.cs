using ThreatIntelligence.Application.Abstractions.Secrets;

namespace ThreatIntelligence.UnitTests.Secrets;

public sealed class ManagedSecretNamesTests
{
    [Fact]
    public void Ioc_chat_key_is_a_supported_identity_distinct_from_report_key()
    {
        Assert.True(ManagedSecretNames.IsSupported(ManagedSecretNames.IocChatOpenAiCompatibleServerApiKey));
        Assert.NotEqual(
            ManagedSecretNames.OpenAiCompatibleServerApiKey,
            ManagedSecretNames.IocChatOpenAiCompatibleServerApiKey);
    }
}
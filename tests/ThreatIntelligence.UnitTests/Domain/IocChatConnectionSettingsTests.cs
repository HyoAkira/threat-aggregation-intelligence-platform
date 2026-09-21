using ThreatIntelligence.Domain.Entities;

namespace ThreatIntelligence.UnitTests.Domain;

public sealed class IocChatConnectionSettingsTests
{
    [Fact]
    public void New_settings_keep_chat_disabled_until_an_admin_enables_it()
    {
        var settings = new IocChatConnectionSettings("https://model.example", "tool-model", false, DateTime.UtcNow);

        Assert.False(settings.Enabled);
        Assert.Equal(1, settings.Id);
        Assert.Equal("https://model.example", settings.BaseUrl);
        Assert.Equal("tool-model", settings.ModelId);
    }
}
namespace ThreatIntelligence.Application.Abstractions.Secrets;

public static class ManagedSecretNames
{
    public const string ThreatFoxApiKey = "ThreatFox.ApiKey";
    public const string PhishTankAppKey = "PhishTank.AppKey";
    public const string OpenAiCompatibleServerApiKey = "OpenAICompatible.ServerApiKey";
    public const string IocChatOpenAiCompatibleServerApiKey = "IocChat.OpenAICompatible.ServerApiKey";

    public static bool IsSupported(string name) => name is
        ThreatFoxApiKey or PhishTankAppKey or OpenAiCompatibleServerApiKey or IocChatOpenAiCompatibleServerApiKey;
}

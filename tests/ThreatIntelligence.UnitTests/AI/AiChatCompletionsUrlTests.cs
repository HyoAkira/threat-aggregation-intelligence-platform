using ThreatIntelligence.Application.AI;
using Xunit;

namespace ThreatIntelligence.UnitTests.AI;

public sealed class AiChatCompletionsUrlTests
{
    [Theory]
    [InlineData("https://api.openai.com", "https://api.openai.com/chat/completions")]
    [InlineData("https://api.openai.com/v1", "https://api.openai.com/v1/chat/completions")]
    [InlineData("https://api.openai.com/v1/", "https://api.openai.com/v1/chat/completions")]
    [InlineData("https://api.openai.com/v1///", "https://api.openai.com/v1/chat/completions")]
    [InlineData("http://localhost:11434", "http://localhost:11434/chat/completions")]
    [InlineData("http://127.0.0.1:8080", "http://127.0.0.1:8080/chat/completions")]
    [InlineData("http://10.0.0.5:8000", "http://10.0.0.5:8000/chat/completions")]
    [InlineData("http://192.168.1.50:1234", "http://192.168.1.50:1234/chat/completions")]
    [InlineData("http://ollama:11434", "http://ollama:11434/chat/completions")]
    [InlineData("http://ollama.internal:11434/v1", "http://ollama.internal:11434/v1/chat/completions")]
    [InlineData("https://gateway.example.org/api/v1", "https://gateway.example.org/api/v1/chat/completions")]
    [InlineData("http://localhost:11434/v1/chat/completions", "http://localhost:11434/v1/chat/completions")]
    [InlineData("HTTP://LocalHost:11434", "http://localhost:11434/chat/completions")]
    public void Appends_the_fixed_operation_path_without_address_filtering(string input, string expected)
    {
        var result = AiChatCompletionsUrl.Build(input);
        Assert.Equal(expected, result.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ollama:11434")]
    [InlineData("ftp://ollama:11434")]
    [InlineData("/relative/path")]
    [InlineData("not a url")]
    [InlineData("https://")]
    public void Rejects_urls_that_are_not_absolute_http_or_https(string input)
    {
        Assert.Throws<ArgumentException>(() => AiChatCompletionsUrl.Build(input));
    }
}

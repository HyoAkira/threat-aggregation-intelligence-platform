using ThreatIntelligence.Infrastructure.AI;

namespace ThreatIntelligence.UnitTests.AI;

public sealed class IocChatAnswerValidatorTests
{
    [Fact]
    public void Allows_natural_refusal_without_fact_or_assessment_labels()
    {
        IocChatAnswerValidator.Validate(
            "I can only help with public IOC and cyber threat intelligence questions.",
            new HashSet<int> { 7 },
            new HashSet<string>());
    }

    [Fact]
    public void Allows_insufficient_evidence_without_fact_or_assessment_labels()
    {
        IocChatAnswerValidator.Validate(
            "Insufficient evidence: the database cannot establish that conclusion.",
            new HashSet<int> { 7 },
            new HashSet<string>());
    }

    [Fact]
    public void Allows_turkish_scope_refusal_without_fact_or_assessment_labels()
    {
        IocChatAnswerValidator.Validate(
            "Bu sohbet yalnızca siber tehdit istihbaratı ve public IOC verileriyle sınırlıdır.",
            new HashSet<int> { 7 },
            new HashSet<string>());
    }

    [Fact]
    public void Requires_evidence_for_substantive_answers()
    {
        var exception = Assert.Throws<IocChatException>(() => IocChatAnswerValidator.Validate(
            "IOC #7 was observed.",
            new HashSet<int> { 7 },
            new HashSet<string>()));

        Assert.Equal("ioc_chat_invalid_answer", exception.Code);
    }

    [Fact]
    public void Allows_natural_evidence_answer_without_fact_label()
    {
        IocChatAnswerValidator.Validate(
            "The indicator was observed by ThreatFox. Evidence: #7 record-7",
            new HashSet<int> { 7 },
            new HashSet<string> { "record-7" });
    }

    [Fact]
    public void Rejects_evidence_that_was_not_retrieved()
    {
        var exception = Assert.Throws<IocChatException>(() => IocChatAnswerValidator.Validate(
            "Assessment: this appears malicious. Evidence: #999",
            new HashSet<int> { 7 },
            new HashSet<string>()));

        Assert.Contains("not retrieved", exception.Message);
    }

    [Fact]
    public void Rejects_prompt_disclosure_in_substantive_answer()
    {
        var exception = Assert.Throws<IocChatException>(() => IocChatAnswerValidator.Validate(
            "Fact: the system prompt is available. Evidence: #7",
            new HashSet<int> { 7 },
            new HashSet<string>()));

        Assert.Contains("protected instructions", exception.Message);
    }
}

using ThreatIntelligence.Application.Abstractions.Persistence;

namespace ThreatIntelligence.Application.AI;

/// <summary>Backend-owned report event contract; the controller serializes these as SSE frames.</summary>
public abstract record AiReportEvent;

public sealed record AiMetaEvent(
    int IocId,
    string ModelId,
    int PolicyVersion,
    int SourceCount,
    int OmittedSourceCount) : AiReportEvent;

public sealed record AiDeltaEvent(string Text) : AiReportEvent;

public sealed record AiCompleteEvent(
    string Report,
    string ModelId,
    int PolicyVersion,
    DateTime GeneratedAtUtc) : AiReportEvent;

public sealed record AiErrorEvent(string Code, string Message) : AiReportEvent;

/// <summary>
/// The fail-closed result of pre-stream validation. <see cref="Error"/> is null when the run may
/// start; otherwise it carries the stable safe error code for a ProblemDetails response.
/// </summary>
public sealed record AiReportPlan(
    IocEvidenceSnapshot Evidence,
    AiReportPolicySnapshot Policy,
    string BaseUrl,
    string ModelId,
    string? Error,
    string? ErrorMessage)
{
    public bool Ok => Error is null;
}

/// <summary>
/// Orchestrates one ephemeral report run: fail-closed configuration checks, version-pinned policy,
/// bounded evidence projection and streaming. Nothing is persisted.
/// </summary>
public sealed class AiReportGenerator(
    IAiConfigurationReader configurationReader,
    IIocQueryReader iocQueryReader,
    IChatCompletionClient chatClient,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Performs every pre-stream check — connection settings, credential availability, policy,
    /// and IOC existence — before any SSE header is written.
    /// </summary>
    public async Task<AiReportPlan> PlanAsync(int iocId, CancellationToken cancellationToken)
    {
        var group = await iocQueryReader.GetDetailGroupAsync(iocId, cancellationToken);
        if (group is null)
        {
            return Fail(AiErrorCodes.NotConfigured, "The requested IOC does not exist.");
        }

        var connection = await configurationReader.GetConnectionAsync(cancellationToken);
        if (connection is null || !connection.Enabled)
        {
            return Fail(AiErrorCodes.Disabled, "AI report generation is disabled.");
        }

        if (string.IsNullOrWhiteSpace(connection.BaseUrl) || string.IsNullOrWhiteSpace(connection.ModelId))
        {
            return Fail(AiErrorCodes.NotConfigured, "AI connection settings are incomplete.");
        }

        try
        {
            _ = AiChatCompletionsUrl.Build(connection.BaseUrl);
        }
        catch (ArgumentException)
        {
            return Fail(AiErrorCodes.InvalidConfiguration, "The configured AI Base URL is not a valid HTTP or HTTPS URL.");
        }

        var policy = await configurationReader.GetActivePolicyAsync(cancellationToken);
        if (policy is null)
        {
            return Fail(AiErrorCodes.NotConfigured, "No report instruction version is published.");
        }

        var evidence = IocEvidenceProjector.Project(group);
        return new AiReportPlan(
            evidence,
            policy,
            connection.BaseUrl!,
            connection.ModelId,
            null,
            null);
    }

    /// <summary>
    /// Streams the planned report as backend-owned events. Deltas are provisional; the terminal
    /// <see cref="AiCompleteEvent"/> is emitted only after final-output validation succeeds.
    /// </summary>
    public async IAsyncEnumerable<AiReportEvent> RunAsync(
        AiReportPlan plan,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new AiMetaEvent(
            plan.Evidence.IocId,
            plan.ModelId,
            plan.Policy.Version,
            plan.Evidence.Sources.Count,
            plan.Evidence.OmittedSourceCount);

        var request = new ChatCompletionRequest(
            plan.BaseUrl,
            plan.ModelId,
            AiReportMessageBuilder.Build(plan.Policy, plan.Evidence),
            Temperature: 0.2,
            MaxTokens: 4000);

        var report = new System.Text.StringBuilder();
        await using var enumerator = chatClient
            .StreamAsync(request, cancellationToken)
            .GetAsyncEnumerator(cancellationToken);
        try
        {
            while (await enumerator.MoveNextAsync())
            {
                report.Append(enumerator.Current.Text);
                yield return new AiDeltaEvent(enumerator.Current.Text);
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        yield return new AiCompleteEvent(
            report.ToString(), plan.ModelId, plan.Policy.Version, timeProvider.GetUtcNow().UtcDateTime);
    }

    private static AiReportPlan Fail(string code, string message) => new(
        new IocEvidenceSnapshot(0, string.Empty, string.Empty, null, null, null, null, [], 0),
        new AiReportPolicySnapshot(0, string.Empty),
        string.Empty,
        string.Empty,
        code,
        message);
}

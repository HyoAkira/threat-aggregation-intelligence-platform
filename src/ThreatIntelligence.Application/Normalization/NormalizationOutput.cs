namespace ThreatIntelligence.Application.Normalization;

public sealed record NormalizationOutput
{
    private NormalizationOutput(NormalizationResult? result, NormalizationError? error)
    {
        Result = result;
        Error = error;
    }

    public NormalizationResult? Result { get; }
    public NormalizationError? Error { get; }

    public bool IsSuccess => Result is not null;
    public bool IsError => Error is not null;

    public static NormalizationOutput Success(NormalizationResult result)
        => new(result, null);

    public static NormalizationOutput Failure(NormalizationErrorCode code, string message)
        => new(null, new NormalizationError(code, message));

    public NormalizationResult OrThrow()
        => Result ?? throw new InvalidOperationException(
            Error?.Message ?? "Normalization failed.");
}

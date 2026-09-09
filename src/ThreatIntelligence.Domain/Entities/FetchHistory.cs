using ThreatIntelligence.Domain.Common;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.Domain.Entities;

public class FetchHistory
{
    private FetchHistory()
    {
    }

    public FetchHistory(
        Guid jobId,
        int providerId,
        FetchTrigger trigger,
        FetchMode mode,
        FetchStatus status,
        DateTime startedAt,
        DateTime? finishedAt = null,
        int recordsAdded = 0,
        int recordsUpdated = 0,
        string? errorDetail = null)
    {
        if (jobId == Guid.Empty)
        {
            throw new ArgumentException("JobId must not be empty.", nameof(jobId));
        }

        JobId = jobId;
        ProviderId = Guard.Positive(providerId, nameof(providerId));
        Trigger = Guard.Defined(trigger, nameof(trigger));
        Mode = Guard.Defined(mode, nameof(mode));
        Status = Guard.Defined(status, nameof(status));
        StartedAt = Guard.Utc(startedAt, nameof(startedAt));
        FinishedAt = Guard.UtcOrNull(finishedAt, nameof(finishedAt));
        RecordsAdded = Guard.NonNegative(recordsAdded, nameof(recordsAdded));
        RecordsUpdated = Guard.NonNegative(recordsUpdated, nameof(recordsUpdated));
        ErrorDetail = errorDetail;

        ValidateStateMachine();
    }

    private void ValidateStateMachine()
    {
        if (FinishedAt.HasValue && FinishedAt < StartedAt)
        {
            throw new InvalidOperationException("FinishedAt must not be earlier than StartedAt.");
        }

        switch (Status)
        {
            case FetchStatus.Queued:
            case FetchStatus.Running:
                if (FinishedAt.HasValue || !string.IsNullOrEmpty(ErrorDetail))
                {
                    throw new InvalidOperationException(
                        $"A {Status} history record must not have a finish time or a terminal error.");
                }

                break;

            case FetchStatus.Success:
                if (!FinishedAt.HasValue)
                {
                    throw new InvalidOperationException("A Success history record requires a finish time.");
                }

                if (!string.IsNullOrEmpty(ErrorDetail))
                {
                    throw new InvalidOperationException("A Success history record must not have a terminal error.");
                }

                break;

            case FetchStatus.Failed:
                if (!FinishedAt.HasValue)
                {
                    throw new InvalidOperationException("A Failed history record requires a finish time.");
                }

                if (string.IsNullOrWhiteSpace(ErrorDetail))
                {
                    throw new InvalidOperationException("A Failed history record requires a terminal error.");
                }

                if (RecordsAdded != 0 || RecordsUpdated != 0)
                {
                    throw new InvalidOperationException("A Failed history record must have zero added and updated counters.");
                }

                break;
        }
    }

    public void Succeed(int recordsAdded, int recordsUpdated, DateTime finishedAtUtc)
    {
        if (Status != FetchStatus.Queued && Status != FetchStatus.Running)
        {
            throw new InvalidOperationException("Only Queued or Running history records can succeed.");
        }

        var validatedRecordsAdded = Guard.NonNegative(recordsAdded, nameof(recordsAdded));
        var validatedRecordsUpdated = Guard.NonNegative(recordsUpdated, nameof(recordsUpdated));
        var validatedFinishedAt = Guard.Utc(finishedAtUtc, nameof(finishedAtUtc));
        if (validatedFinishedAt < StartedAt)
        {
            throw new InvalidOperationException("FinishedAt must not be earlier than StartedAt.");
        }

        RecordsAdded = validatedRecordsAdded;
        RecordsUpdated = validatedRecordsUpdated;
        FinishedAt = validatedFinishedAt;
        Status = FetchStatus.Success;
        ErrorDetail = null;
    }

    public void Fail(string errorDetail, DateTime finishedAtUtc)
    {
        if (Status != FetchStatus.Queued && Status != FetchStatus.Running)
        {
            throw new InvalidOperationException("Only Queued or Running history records can fail.");
        }

        var validatedFinishedAt = Guard.Utc(finishedAtUtc, nameof(finishedAtUtc));
        if (validatedFinishedAt < StartedAt)
        {
            throw new InvalidOperationException("FinishedAt must not be earlier than StartedAt.");
        }

        var validatedErrorDetail = Guard.Required(errorDetail, nameof(errorDetail));

        FinishedAt = validatedFinishedAt;
        Status = FetchStatus.Failed;
        RecordsAdded = 0;
        RecordsUpdated = 0;
        ErrorDetail = validatedErrorDetail;
    }

    public int Id { get; private set; }
    public Guid JobId { get; private set; }
    public int ProviderId { get; private set; }
    public FetchTrigger Trigger { get; private set; }
    public FetchMode Mode { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public FetchStatus Status { get; private set; }
    public int RecordsAdded { get; private set; }
    public int RecordsUpdated { get; private set; }
    public string? ErrorDetail { get; private set; }
}

using System.Text.Json;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Domain.Enums;

namespace ThreatIntelligence.UnitTests.Fetches;

public sealed class FetchJobTests
{
    [Fact]
    public void Golden_message_round_trips_with_the_shared_contract()
    {
        var job = new FetchJob(
            Guid.Parse("5e1c0a80-cf9b-4c8e-b9c7-462b30cf7614"),
            1,
            FetchTrigger.Manual,
            FetchMode.Full,
            1);

        var json = JsonSerializer.Serialize(job);
        var restored = JsonSerializer.Deserialize<FetchJob>(json);

        Assert.Equal("{\"JobId\":\"5e1c0a80-cf9b-4c8e-b9c7-462b30cf7614\",\"ProviderId\":1,\"Trigger\":\"manual\",\"Mode\":\"full\",\"Attempt\":1,\"CorrelationId\":\"5e1c0a80cf9b4c8eb9c7462b30cf7614\"}", json);
        Assert.Equal(job, restored);
    }

    [Fact]
    public void Correlation_id_defaults_to_the_job_and_round_trips_when_supplied()
    {
        var jobId = Guid.NewGuid();
        var defaulted = new FetchJob(jobId, 1, FetchTrigger.Manual, FetchMode.Recent, 1);
        Assert.Equal(jobId.ToString("N"), defaulted.CorrelationId);

        var supplied = new FetchJob(jobId, 1, FetchTrigger.Manual, FetchMode.Recent, 1, "corr-123");
        var restored = JsonSerializer.Deserialize<FetchJob>(JsonSerializer.Serialize(supplied));
        Assert.Equal("corr-123", restored!.CorrelationId);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void Invalid_numeric_fields_are_rejected(int providerId, int attempt)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FetchJob(Guid.NewGuid(), providerId, FetchTrigger.Manual, FetchMode.Recent, attempt));
    }

    [Fact]
    public void Undefined_enums_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FetchJob(Guid.NewGuid(), 1, (FetchTrigger)99, FetchMode.Recent, 1));
    }
}

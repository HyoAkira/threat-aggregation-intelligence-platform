using System.Net.Http.Headers;
using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using ThreatIntelligence.Application.Fetches;
using ThreatIntelligence.Domain.Enums;
using ThreatIntelligence.Infrastructure.Fetches;

namespace ThreatIntelligence.IntegrationTests;

[Collection("rabbitmq")]
public sealed class FetchBrokerTests(RabbitMqFixture fixture)
{
    [Fact]
    public async Task Transient_failure_retries_then_dead_letters_and_marks_failed()
    {
        var probe = new FetchProbe { Behavior = (_, _) => throw new TransientFetchException("temporary upstream failure") };
        await using var host = await BrokerTestHost.StartAsync(fixture, probe, [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)]);
        var jobId = Guid.NewGuid();

        await host.PublishAsync(new FetchJob(jobId, 1, FetchTrigger.Manual, FetchMode.Recent, 1));
        var deadLettered = await probe.DeadLettered.Task.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Equal(jobId, deadLettered);
        Assert.Equal(4, Volatile.Read(ref probe.Started));
        Assert.Contains(probe.Failed, failure => failure.JobId == jobId);
        Assert.DoesNotContain(jobId, probe.Succeeded);
    }

    [Fact]
    public async Task Terminal_failure_runs_once_without_dead_letter()
    {
        var probe = new FetchProbe { Behavior = (_, _) => throw new TerminalFetchException("invalid provider record") };
        await using var host = await BrokerTestHost.StartAsync(fixture, probe, [TimeSpan.FromSeconds(1)]);
        var jobId = Guid.NewGuid();

        await host.PublishAsync(new FetchJob(jobId, 1, FetchTrigger.Manual, FetchMode.Recent, 1));
        await probe.FailedSignal.Task.WaitAsync(TimeSpan.FromSeconds(15));

        Assert.Equal(1, Volatile.Read(ref probe.Started));
        Assert.Contains(probe.Failed, failure => failure.JobId == jobId);
        Assert.False(probe.DeadLettered.Task.IsCompleted);
    }

    [Fact]
    public async Task Declared_job_queue_is_durable()
    {
        var probe = new FetchProbe();
        await using var host = await BrokerTestHost.StartAsync(fixture, probe, [TimeSpan.FromSeconds(1)]);

        Assert.True(await host.GetQueueDurableAsync(host.QueueName));
    }

    [Fact]
    public async Task Only_one_fetch_executes_at_a_time()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;
        var probe = new FetchProbe
        {
            Behavior = async (_, _) =>
            {
                if (Interlocked.Increment(ref started) == 1)
                {
                    await gate.Task;
                }
                else
                {
                    secondStarted.TrySetResult();
                }

                return new FetchExecutionResult(0, 0);
            }
        };
        await using var host = await BrokerTestHost.StartAsync(fixture, probe, [TimeSpan.FromSeconds(1)]);

        await host.PublishAsync(new FetchJob(Guid.NewGuid(), 1, FetchTrigger.Manual, FetchMode.Recent, 1));
        await probe.FirstStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await host.PublishAsync(new FetchJob(Guid.NewGuid(), 1, FetchTrigger.Manual, FetchMode.Recent, 1));

        await Task.Delay(TimeSpan.FromSeconds(2));
        Assert.Equal(1, Volatile.Read(ref started));

        gate.TrySetResult();
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(2, Volatile.Read(ref started));
    }

    private sealed class FetchProbe
    {
        private readonly object _sync = new();

        public Func<FetchJob, CancellationToken, Task<FetchExecutionResult>>? Behavior { get; init; }

        public int Started;

        public List<Guid> Succeeded { get; } = [];

        public List<(Guid JobId, string Error)> Failed { get; } = [];

        public TaskCompletionSource<Guid> FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<Guid> DeadLettered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource FailedSignal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void RecordSuccess(Guid jobId)
        {
            lock (_sync)
            {
                Succeeded.Add(jobId);
            }
        }

        public void RecordFailure(Guid jobId, string error)
        {
            lock (_sync)
            {
                Failed.Add((jobId, error));
            }

            FailedSignal.TrySetResult();
        }
    }

    private sealed class ProbeLifecycle(FetchProbe probe) : IFetchLifecycle
    {
        public Task<bool> TryStartAsync(FetchJob job, bool isRetry, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task SucceedAsync(FetchJob job, int recordsAdded, int recordsUpdated, CancellationToken cancellationToken)
        {
            probe.RecordSuccess(job.JobId);
            return Task.CompletedTask;
        }

        public Task FailAsync(FetchJob job, string errorDetail, CancellationToken cancellationToken)
        {
            probe.RecordFailure(job.JobId, errorDetail);
            return Task.CompletedTask;
        }
    }

    private sealed class ProbeExecutor(FetchProbe probe) : IProviderFetchExecutor
    {
        public async Task<FetchExecutionResult> ExecuteAsync(FetchJob job, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref probe.Started);
            probe.FirstStarted.TrySetResult(job.JobId);
            return probe.Behavior is null
                ? new FetchExecutionResult(0, 0)
                : await probe.Behavior(job, cancellationToken);
        }
    }

    private sealed class ErrorObserver(FetchProbe probe) : IConsumer<FetchJob>
    {
        public Task Consume(ConsumeContext<FetchJob> context)
        {
            probe.DeadLettered.TrySetResult(context.Message.JobId);
            return Task.CompletedTask;
        }
    }

    private sealed class BrokerTestHost : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IBusControl _bus;
        private readonly RabbitMqFixture _fixture;

        private BrokerTestHost(ServiceProvider provider, IBusControl bus, RabbitMqFixture fixture, string queueName)
        {
            _provider = provider;
            _bus = bus;
            _fixture = fixture;
            QueueName = queueName;
        }

        public string QueueName { get; }

        public static async Task<BrokerTestHost> StartAsync(RabbitMqFixture fixture, FetchProbe probe, TimeSpan[] retryIntervals)
        {
            var queueName = $"tieto.test.{Guid.NewGuid():N}";
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(probe);
            services.AddScoped<IFetchLifecycle, ProbeLifecycle>();
            services.AddScoped<IProviderFetchExecutor, ProbeExecutor>();
            services.AddMassTransit(configurator =>
            {
                configurator.AddConsumer<FetchJobConsumer>();
                configurator.AddConsumer<ErrorObserver>();
                configurator.UsingRabbitMq((context, bus) =>
                {
                    bus.Host(fixture.AmqpUri, host =>
                    {
                        host.Username(fixture.User);
                        host.Password(fixture.Password);
                    });
                    bus.ReceiveEndpoint(queueName, endpoint =>
                    {
                        endpoint.PrefetchCount = 1;
                        endpoint.ConcurrentMessageLimit = 1;
                        endpoint.UseMessageRetry(retry => retry.Intervals(retryIntervals).Handle<TransientFetchException>());
                        endpoint.ConfigureConsumer<FetchJobConsumer>(context);
                    });
                    bus.ReceiveEndpoint($"{queueName}_error", endpoint =>
                    {
                        endpoint.ConfigureConsumeTopology = false;
                        endpoint.ConfigureConsumer<ErrorObserver>(context);
                    });
                });
            });

            var provider = services.BuildServiceProvider();
            var bus = provider.GetRequiredService<IBusControl>();
            await bus.StartAsync();
            return new BrokerTestHost(provider, bus, fixture, queueName);
        }

        public Task PublishAsync(FetchJob job) => _bus.Publish(job);

        public async Task<bool> GetQueueDurableAsync(string queueName)
        {
            using var client = CreateManagementClient();
            using var response = await client.GetAsync($"api/queues/%2F/{queueName}");
            response.EnsureSuccessStatusCode();
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return document.RootElement.GetProperty("durable").GetBoolean();
        }

        public async ValueTask DisposeAsync()
        {
            await _bus.StopAsync();
            await _provider.DisposeAsync();
            await DeleteQueueAsync($"{QueueName}_error");
            await DeleteQueueAsync(QueueName);
        }

        private async Task DeleteQueueAsync(string queueName)
        {
            using var client = CreateManagementClient();
            await client.DeleteAsync($"api/queues/%2F/{queueName}");
        }

        private HttpClient CreateManagementClient()
        {
            var client = new HttpClient { BaseAddress = new Uri($"http://{_fixture.Host}:{_fixture.ManagementPort}/") };
            var credentials = Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{_fixture.User}:{_fixture.Password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            return client;
        }
    }
}

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Freeside.Core.Bitcoin;
using Freeside.Core.Messaging;
using Freeside.Core.Persistence;
using Freeside.Infrastructure.Database;
using Freeside.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Freeside.Infrastructure.Tests.Messaging;

/// <summary>The hosted job runner, inbox processor and outbox dispatcher, end to end.</summary>
[Collection(nameof(PostgresDatabase))]
public sealed class QueueProcessorTests(PostgresFixture postgres)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_job_runner_runs_each_job_once_with_four_loops()
    {
        var type = Queues.UniqueKey("test.hosted-jobs");
        var recorder = new Recorder();
        using var host = BuildHost(services => services.AddScoped<IJobHandler>(_ => new JobHandler(type, recorder)), concurrency: 4);
        await host.StartAsync(Ct);

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var scheduler = scope.ServiceProvider.GetRequiredService<IJobScheduler>();
            for (var i = 0; i < 100; i++)
            {
                await scheduler.ScheduleAsync(type, $$"""{"n":{{i}}}""", correlationId: $"corr-{i}", cancellationToken: Ct);
            }
        }

        await Queues.WaitUntilAsync(
            async () => await Queues.CountAsync(postgres, "jobs WHERE job_type = @type AND status = 'Succeeded'", ("type", type)) == 100,
            "all 100 jobs succeed");
        await host.StopAsync(Ct);

        Assert.Equal(100, recorder.Items.Count);
        Assert.Equal(100, recorder.Items.Select(item => item.Id).Distinct().Count());
        Assert.All(recorder.Items, item =>
        {
            using var payload = JsonDocument.Parse(item.Payload);
            Assert.Equal($"corr-{payload.RootElement.GetProperty("n").GetInt32()}", item.CorrelationId);
            Assert.Equal((type, 1), (item.Type, item.Attempt));
        });
    }

    [Fact]
    public async Task The_inbox_processor_hands_the_raw_body_to_the_handler_for_its_source()
    {
        var source = Queues.UniqueKey("test.hosted-inbox");
        const string rawBody = """{"deliveryId":"d1",  "type":"InvoiceSettled"}""";
        var recorder = new Recorder();
        using var host = BuildHost(services => services.AddScoped<IInboxHandler>(_ => new InboxHandler(source, recorder)));
        await host.StartAsync(Ct);

        await using (var scope = host.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<IInbox>()
                .AcceptAsync(source, "d1", "InvoiceSettled", rawBody, "corr-inbox", Ct));
        }

        await Queues.WaitUntilAsync(() => Task.FromResult(!recorder.Items.IsEmpty), "the inbox handler runs");
        await Queues.WaitUntilAsync(
            async () => await Queues.CountAsync(postgres, "inbox_messages WHERE source = @source AND status = 'Succeeded'", ("source", source)) == 1,
            "the message succeeds");
        await host.StopAsync(Ct);

        var item = Assert.Single(recorder.Items);
        Assert.Equal(("InvoiceSettled", rawBody, "corr-inbox"), (item.Type, item.Payload, item.CorrelationId));
    }

    [Fact]
    public async Task The_outbox_dispatcher_sends_what_a_unit_of_work_committed()
    {
        var type = Queues.UniqueKey("test.hosted-outbox");
        var recorder = new Recorder();
        using var host = BuildHost(services => services.AddScoped<IOutboxHandler>(_ => new OutboxHandler(type, recorder)));
        await host.StartAsync(Ct);

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteAsync(_ =>
            {
                outbox.Add(type, """{"receipt":"r1"}""", "corr-outbox");
                return Task.CompletedTask;
            }, Ct);
        }

        await Queues.WaitUntilAsync(
            async () => await Queues.CountAsync(postgres, "outbox_messages WHERE message_type = @type AND status = 'Succeeded'", ("type", type)) == 1,
            "the message is dispatched");
        await host.StopAsync(Ct);

        var item = Assert.Single(recorder.Items);
        Assert.Equal((type, "corr-outbox"), (item.Type, item.CorrelationId));
    }

    [Fact]
    public async Task A_handler_that_keeps_failing_leaves_the_job_Dead_with_its_error()
    {
        var type = Queues.UniqueKey("test.hosted-failing");
        var recorder = new Recorder();
        using var host = BuildHost(
            services => services.AddScoped<IJobHandler>(_ => new JobHandler(type, recorder, fail: true)),
            maxAttempts: 3);
        await host.StartAsync(Ct);
        var id = await Queues.ScheduleAsync(postgres, type, maxAttempts: 3);

        await Queues.WaitUntilAsync(async () => (await Queues.RowAsync(postgres, QueueDefinition.Jobs, id)).Status == "Dead", "the job is Dead");
        await host.StopAsync(Ct);

        Assert.Equal([1, 2, 3], recorder.Items.Select(item => item.Attempt).Order());
        var row = await Queues.RowAsync(postgres, QueueDefinition.Jobs, id);
        Assert.Equal(3, row.Attempts);
        Assert.Equal("System.InvalidOperationException: Handler failed on attempt 3.", row.LastError);
    }

    [Fact]
    public async Task Stopping_the_worker_releases_an_unfinished_job_without_counting_the_attempt()
    {
        var type = Queues.UniqueKey("test.hosted-stop");
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var host = BuildHost(services => services.AddScoped<IJobHandler>(_ => new BlockingJobHandler(type, started)));
        await host.StartAsync(Ct);
        var id = await Queues.ScheduleAsync(postgres, type);

        await started.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        await host.StopAsync(Ct);

        Assert.Equal(new QueueRow("Pending", 0, null, null, false, false), await Queues.RowAsync(postgres, QueueDefinition.Jobs, id));
    }

    [Fact]
    public async Task Two_handlers_for_the_same_job_type_stop_the_runner()
    {
        var type = Queues.UniqueKey("test.hosted-duplicate");
        var recorder = new Recorder();
        using var host = BuildHost(services =>
        {
            services.AddScoped<IJobHandler>(_ => new JobHandler(type, recorder));
            services.AddScoped<IJobHandler>(_ => new JobHandler(type, recorder));
        });
        var runner = host.Services.GetServices<IHostedService>().OfType<JobRunner>().Single();

        await host.StartAsync(Ct);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(30), Ct));
        Assert.Contains(type, ex.Message, StringComparison.Ordinal);
        await host.StopAsync(Ct);
    }

    private IHost BuildHost(Action<IServiceCollection> addHandlers, int maxAttempts = 10, int concurrency = 1)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bitcoin:Network"] = "regtest",
            ["Database:ConnectionString"] = postgres.AppConnectionString,
            ["Database:Authentication"] = "Password",
            ["WorkQueues:PollInterval"] = "00:00:00.050",
            ["WorkQueues:MaxAttempts"] = maxAttempts.ToString(CultureInfo.InvariantCulture),
            ["WorkQueues:BackoffBase"] = "00:00:00.001",
            ["WorkQueues:BackoffCap"] = "00:00:00.010",
            ["WorkQueues:Concurrency"] = concurrency.ToString(CultureInfo.InvariantCulture),
        });
        builder.Services.AddBitcoinNetwork();
        builder.Services.AddFreesideDatabase();
        builder.Services.AddFreesideQueueProcessors();
        addHandlers(builder.Services);
        return builder.Build();
    }

    private sealed class Recorder
    {
        public ConcurrentQueue<WorkItem> Items { get; } = new();
    }

    private sealed class JobHandler(string jobType, Recorder recorder, bool fail = false) : IJobHandler
    {
        public string JobType => jobType;

        public Task HandleAsync(WorkItem job, CancellationToken cancellationToken)
        {
            recorder.Items.Enqueue(job);
            return fail
                ? throw new InvalidOperationException($"Handler failed on attempt {job.Attempt}.")
                : Task.CompletedTask;
        }
    }

    private sealed class BlockingJobHandler(string jobType, TaskCompletionSource started) : IJobHandler
    {
        public string JobType => jobType;

        public async Task HandleAsync(WorkItem job, CancellationToken cancellationToken)
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    private sealed class InboxHandler(string source, Recorder recorder) : IInboxHandler
    {
        public string Source => source;

        public Task HandleAsync(WorkItem message, CancellationToken cancellationToken)
        {
            recorder.Items.Enqueue(message);
            return Task.CompletedTask;
        }
    }

    private sealed class OutboxHandler(string messageType, Recorder recorder) : IOutboxHandler
    {
        public string MessageType => messageType;

        public Task HandleAsync(WorkItem message, CancellationToken cancellationToken)
        {
            recorder.Items.Enqueue(message);
            return Task.CompletedTask;
        }
    }
}

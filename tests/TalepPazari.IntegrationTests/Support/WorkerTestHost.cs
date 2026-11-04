using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TalepPazari.Modules.Example.Application.ExampleItems.CreateExampleItem;
using TalepPazari.Worker;
using TalepPazari.Worker.Outbox;

namespace TalepPazari.IntegrationTests.Support;

// The real Worker host composition (WorkerComposition.AddWorkerServices, as Program.cs uses it), built but
// not started: tests run dispatcher passes directly. Every log entry is captured at every level.
public sealed class WorkerTestHost : IAsyncDisposable
{
    private readonly IHost host;

    private WorkerTestHost(IHost host, TestLoggerProvider logs)
    {
        this.host = host;
        Logs = logs;
    }

    public TestLoggerProvider Logs { get; }

    public IServiceProvider Services => host.Services;

    public static WorkerTestHost Create(string connectionString, IReadOnlyDictionary<string, string?>? settings = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = "Testing",
            ContentRootPath = AppContext.BaseDirectory,
        });
        var values = new Dictionary<string, string?>(settings ?? ExampleTestHost.DefaultOutboxSettings)
        {
            ["ConnectionStrings:Default"] = connectionString,
        };
        builder.Configuration.AddInMemoryCollection(values);
        builder.AddWorkerServices();

        var logs = new TestLoggerProvider();
        builder.Logging.AddProvider(logs).AddFilter<TestLoggerProvider>(null, LogLevel.Trace);
        return new WorkerTestHost(builder.Build(), logs);
    }

    public Task<OutboxDispatchResult> RunOnceAsync() =>
        Services.GetRequiredService<OutboxDispatcher>().RunOnceAsync(TestContext.Current.CancellationToken);

    public async Task CreateItemAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<CreateExampleItemHandler>()
            .HandleAsync(new CreateExampleItemCommand("item"), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
    }

    public async ValueTask DisposeAsync()
    {
        if (host is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            host.Dispose();
        }
    }
}

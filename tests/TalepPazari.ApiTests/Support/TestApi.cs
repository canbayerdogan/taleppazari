using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TalepPazari.IntegrationTests.Support;

namespace TalepPazari.ApiTests.Support;

// The real Api host (Program.cs) in memory. Environment "Testing": not Development, so user secrets,
// the developer exception page and the OpenAPI endpoint are off, as outside Development.
public sealed class TestApi : IAsyncDisposable
{
    // Never opened: tests that need no database still resolve a DbContext (handlers depend on one).
    public const string PlaceholderConnectionString =
        "Host=127.0.0.1;Port=1;Database=never_opened;Username=placeholder;Password=placeholder";

    private readonly WebApplicationFactory<Program> root;
    private readonly WebApplicationFactory<Program> factory;

    private TestApi(string connectionString, Action<IServiceCollection>? configureServices, IReadOnlyDictionary<string, string?>? settings)
    {
        root = new WebApplicationFactory<Program>();
        factory = root.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");

            // Added last, so it wins over environment variables of the machine.
            var values = new Dictionary<string, string?>(settings ?? new Dictionary<string, string?>())
            {
                ["ConnectionStrings:Default"] = connectionString,
            };
            builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(values));

            // Every entry of every category at every level, whatever appsettings.json filters.
            builder.ConfigureLogging(logging => logging
                .AddProvider(Logs)
                .AddFilter<TestLoggerProvider>(null, LogLevel.Trace));

            if (configureServices is not null)
            {
                builder.ConfigureTestServices(configureServices);
            }
        });
        Client = factory.CreateClient();
    }

    public HttpClient Client { get; }

    public TestLoggerProvider Logs { get; } = new();

    public IServiceProvider Services => factory.Services;

    // settings: extra configuration values (for example Logging:LogLevel entries), added after appsettings.json.
    public static TestApi Create(
        string? connectionString = null,
        Action<IServiceCollection>? configureServices = null,
        IReadOnlyDictionary<string, string?>? settings = null) =>
        new(connectionString ?? PlaceholderConnectionString, configureServices, settings);

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await factory.DisposeAsync();
        await root.DisposeAsync();
    }
}

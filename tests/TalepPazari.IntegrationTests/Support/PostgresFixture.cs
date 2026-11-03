using DotNet.Testcontainers.Builders;
using Npgsql;
using Testcontainers.PostgreSql;

namespace TalepPazari.IntegrationTests.Support;

// One PostgreSQL container per fixture (ADR-005): per test run in this project (assembly fixture,
// AssemblyFixtures.cs); per test class in TalepPazari.ApiTests, which links this file.
// Every test gets its own database, so tests never touch the developer's local data
// and never see each other's rows.
public sealed class PostgresFixture : IAsyncLifetime
{
    // Same image as compose.yaml.
    private const string Image = "postgis/postgis:18-3.6";

    // Set to "true" (CI, T-009) to fail instead of skip when Docker is not available.
    public const string RequireDockerVariable = "TALEPPAZARI_REQUIRE_DOCKER";

    private PostgreSqlContainer? container;
    private string? skipReason;

    public async ValueTask InitializeAsync()
    {
        try
        {
            container = new PostgreSqlBuilder(Image).Build();
            await container.StartAsync();
        }
        catch (DockerUnavailableException exception) when (!DockerRequired())
        {
            // Only "no Docker endpoint" is turned into a skip. Image pull, container start and every
            // other failure propagate and fail the tests.
            skipReason = "Docker is not available, so the PostgreSQL integration tests are skipped "
                + $"({exception.Message.Split('\n')[0].Trim()}). Set {RequireDockerVariable}=true to fail instead.";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    // Creates an empty database for one test and returns its connection string.
    public async Task<string> CreateDatabaseAsync()
    {
        if (skipReason is not null)
        {
            Assert.Skip(skipReason);
        }

        var serverConnectionString = container!.GetConnectionString();
        var database = $"t_{Guid.CreateVersion7():N}";

        await using (var connection = new NpgsqlConnection(serverConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
            await command.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = database }.ConnectionString;
    }

    private static bool DockerRequired() =>
        string.Equals(Environment.GetEnvironmentVariable(RequireDockerVariable), "true", StringComparison.OrdinalIgnoreCase);
}

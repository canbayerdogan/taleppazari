using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using TalepPazari.ApiTests.Support;

namespace TalepPazari.ApiTests.Snapshots;

// Guards the Api host's OpenAPI document (ADR-005, ADR-006). The document is generated in memory from the
// host's services, so the test does not depend on the Development-only /openapi/v1.json endpoint.
//
// On a mismatch the test fails and writes openapi-v1.received.json to the test output directory (never into
// the source tree). Deliberate update, locally only: set TALEPPAZARI_UPDATE_SNAPSHOTS=true, run this test
// (it rewrites Snapshots/openapi-v1.json and reports itself as skipped), review the diff and commit it.
// With CI=true or CI=1 (any case) the update is refused.
public sealed class OpenApiSnapshotTests
{
    public const string UpdateVariable = "TALEPPAZARI_UPDATE_SNAPSHOTS";
    private const string SnapshotName = "openapi-v1.json";

    [Fact]
    public async Task OpenApi_document_matches_the_committed_snapshot()
    {
        var actual = await GenerateDocumentAsync();

        if (IsTrue(UpdateVariable))
        {
            Assert.False(IsCi(), $"{UpdateVariable} is refused when CI is set (true or 1): snapshots are updated locally and reviewed.");
            await File.WriteAllTextAsync(SourceSnapshotPath(), actual, TestContext.Current.CancellationToken);
            Assert.Skip($"{SnapshotName} was rewritten because {UpdateVariable}=true. Review the diff, commit it, and run again without the variable.");
        }

        var expectedPath = Path.Combine(AppContext.BaseDirectory, "Snapshots", SnapshotName);
        var expected = File.Exists(expectedPath) ? Normalize(await File.ReadAllTextAsync(expectedPath, TestContext.Current.CancellationToken)) : null;
        if (expected != actual)
        {
            var receivedPath = Path.Combine(AppContext.BaseDirectory, "Snapshots", "openapi-v1.received.json");
            Directory.CreateDirectory(Path.GetDirectoryName(receivedPath)!);
            await File.WriteAllTextAsync(receivedPath, actual, TestContext.Current.CancellationToken);
            Assert.Fail(
                $"The OpenAPI document differs from Snapshots/{SnapshotName}. The generated document was written to "
                + $"openapi-v1.received.json in the test output directory. If the API change is deliberate, set "
                + $"{UpdateVariable}=true locally, run this test, review the diff and commit it.");
        }
    }

    private static async Task<string> GenerateDocumentAsync()
    {
        await using var api = TestApi.Create();
        var provider = api.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1");
        var document = await provider.GetOpenApiDocumentAsync(TestContext.Current.CancellationToken);
        var json = await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_1, TestContext.Current.CancellationToken);
        return Normalize(json);
    }

    // Line endings depend on the checkout (Git autocrlf), not on the API.
    private static string Normalize(string json) => json.ReplaceLineEndings("\n").TrimEnd('\n') + "\n";

    private static bool IsTrue(string variable) =>
        string.Equals(Environment.GetEnvironmentVariable(variable), "true", StringComparison.OrdinalIgnoreCase);

    // CI systems set CI=true (GitHub Actions) or CI=1 (some others).
    private static bool IsCi() =>
        IsTrue("CI") || string.Equals(Environment.GetEnvironmentVariable("CI")?.Trim(), "1", StringComparison.Ordinal);

    // The project folder above the test output directory (bin/<configuration>/<framework>/). Not taken from
    // [CallerFilePath], which deterministic CI builds map to a placeholder path.
    private static string SourceSnapshotPath()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TalepPazari.ApiTests.csproj")))
            {
                return Path.Combine(directory.FullName, "Snapshots", SnapshotName);
            }
        }

        throw new InvalidOperationException("TalepPazari.ApiTests.csproj not found above the test output directory.");
    }
}

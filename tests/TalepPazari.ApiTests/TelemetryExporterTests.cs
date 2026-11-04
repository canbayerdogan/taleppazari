using TalepPazari.ApiTests.Support;

namespace TalepPazari.ApiTests;

// Telemetry:Exporter: Console when unset (the default), None exports nothing, anything else stops the host.
// The console exporter writes to Console.Out, which these tests redirect, so they run alone (serial collection).
[Collection(ConsoleCaptureCollection.Name)]
public sealed class TelemetryExporterTests
{
    // The console trace exporter prints this label for every exported span.
    private const string ExportedSpanMarker = "Activity.TraceId:";

    [Fact]
    public async Task Unset_exporter_defaults_to_Console_and_exports_request_spans()
    {
        var output = await CaptureRequestAsync(exporter: null);

        Assert.Contains(ExportedSpanMarker, output, StringComparison.Ordinal);
        Assert.Contains("/api/v1/example/items", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task None_exports_nothing()
    {
        var output = await CaptureRequestAsync(exporter: "None");

        Assert.DoesNotContain(ExportedSpanMarker, output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_exporter_stops_the_host()
    {
        var error = Assert.ThrowsAny<Exception>(() =>
        {
            using var _ = TestApi.Create(settings: new Dictionary<string, string?> { ["Telemetry:Exporter"] = "Vendor" }).Client;
        });

        Assert.Contains(ExceptionChain(error), e => e.Message.Contains("Telemetry:Exporter", StringComparison.Ordinal));
    }

    private static async Task<string> CaptureRequestAsync(string? exporter)
    {
        var original = Console.Out;
        await using var captured = new StringWriter();
        Console.SetOut(TextWriter.Synchronized(captured));
        try
        {
            await using var api = TestApi.Create(settings: new Dictionary<string, string?> { ["Telemetry:Exporter"] = exporter });
            using var response = await api.Client.GetAsync("/api/v1/example/items?limit=0", TestContext.Current.CancellationToken);
            Assert.Equal(400, (int)response.StatusCode);

            // The request activity stops (and is exported) just after the client has the response: wait for it,
            // or, when nothing should be exported, give it the same chance to appear.
            var deadline = DateTime.UtcNow + (exporter is null ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(1));
            while (DateTime.UtcNow < deadline && !captured.ToString().Contains(ExportedSpanMarker, StringComparison.Ordinal))
            {
                await Task.Delay(50, TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            Console.SetOut(original);
        }

        return captured.ToString();
    }

    private static IEnumerable<Exception> ExceptionChain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleCaptureCollection
{
    public const string Name = "Console capture";
}

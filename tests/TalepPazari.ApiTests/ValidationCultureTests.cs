using System.Globalization;
using System.Net.Http.Json;
using TalepPazari.ApiTests.Support;

namespace TalepPazari.ApiTests;

// Changes the process-wide default UI culture, so it runs alone (no parallel test sees it).
[CollectionDefinition(nameof(ProcessCultureCollection), DisableParallelization = true)]
public sealed class ProcessCultureCollection;

[Collection(nameof(ProcessCultureCollection))]
public sealed class ValidationCultureTests
{
    [Fact]
    public async Task Validation_messages_are_the_same_under_Turkish_and_invariant_server_culture()
    {
        var invariant = await NameErrorsUnderAsync(CultureInfo.InvariantCulture);
        var turkish = await NameErrorsUnderAsync(CultureInfo.GetCultureInfo("tr-TR"));

        Assert.Equal(invariant, turkish);
        Assert.Equal("[\"'Name' must not be empty.\"]", turkish);
    }

    // The server thread culture, not a client header: what a server with a Turkish OS locale would use.
    private static async Task<string> NameErrorsUnderAsync(CultureInfo culture)
    {
        var previous = CultureInfo.DefaultThreadCurrentUICulture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        try
        {
            await using var api = TestApi.Create();
            var response = await api.Client.PostAsJsonAsync(
                "/api/v1/example/items", new { name = "" }, TestContext.Current.CancellationToken);

            var problem = await ProblemAssert.HasShapeAsync(response, 400, withErrors: true);
            return problem.GetProperty("errors").GetProperty("Name").GetRawText();
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentUICulture = previous;
        }
    }
}

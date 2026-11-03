using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace TalepPazari.Api.OpenApi;

// Keeps the generated document free of machine data, so the snapshot test compares only the API surface.
internal sealed class DeterministicDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        // Server URLs depend on where the host runs; clients use the URL they already called.
        document.Servers = null;
        return Task.CompletedTask;
    }
}

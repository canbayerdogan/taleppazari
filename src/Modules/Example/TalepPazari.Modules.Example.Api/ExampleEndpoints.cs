using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TalepPazari.Modules.Example.Application.ExampleItems.CreateExampleItem;

namespace TalepPazari.Modules.Example.Api;

public static class ExampleEndpoints
{
    // Route literal for now; T-008 owns the final API convention.
    public static IEndpointRouteBuilder MapExampleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/example");
        group.MapPost("/items", CreateItemAsync);
        return endpoints;
    }

    // Maps HTTP to the use case and back; no business rules here.
    private static async Task<Results<Created<ExampleItemResponse>, ValidationProblem>> CreateItemAsync(
        CreateExampleItemCommand command,
        CreateExampleItemHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.ValidationProblem(result.ValidationErrors);
        }

        // No Location header: nothing serves the item URL yet.
        return TypedResults.Created((string?)null, result.Item);
    }
}

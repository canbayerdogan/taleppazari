using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TalepPazari.Modules.Example.Application.ExampleItems.CreateExampleItem;
using TalepPazari.Modules.Example.Application.ExampleItems.ListExampleItems;

namespace TalepPazari.Modules.Example.Api;

public static class ExampleEndpoints
{
    // The host passes its versioned route group; the module adds only its own prefix.
    public static IEndpointRouteBuilder MapExampleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/example").WithTags("Example");

        // Anonymous on purpose: Identity does not exist yet. Real endpoints authorize per resource in Application (Phase 2).
        group.MapPost("/items", CreateItemAsync).WithName("CreateExampleItem");
        group.MapGet("/items", ListItemsAsync).WithName("ListExampleItems");
        return endpoints;
    }

    // Maps the HTTP request to the use case and the result back to HTTP; no business rules here.
    private static async Task<Results<Created<ExampleItemResponse>, ValidationProblem>> CreateItemAsync(
        CreateExampleItemRequest request,
        CreateExampleItemHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new CreateExampleItemCommand(request.Name), cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.ValidationProblem(result.ValidationErrors);
        }

        var response = new ExampleItemResponse(result.Item.Id, result.Item.Name, result.Item.CreatedAt);

        // No Location header: nothing serves the item URL yet.
        return TypedResults.Created((string?)null, response);
    }

    // Cursor pagination (docs/architecture/api-conventions.md): ?limit=1..100 (default 20)&cursor=<nextCursor>.
    private static async Task<Results<Ok<ExampleItemPageResponse>, ValidationProblem>> ListItemsAsync(
        int? limit,
        string? cursor,
        ListExampleItemsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ListExampleItemsQuery(limit, cursor), cancellationToken);
        if (!result.IsSuccess)
        {
            return TypedResults.ValidationProblem(result.ValidationErrors);
        }

        var items = result.Page.Items
            .Select(item => new ExampleItemResponse(item.Id, item.Name, item.CreatedAt))
            .ToList();
        return TypedResults.Ok(new ExampleItemPageResponse(items, result.Page.NextCursor));
    }
}

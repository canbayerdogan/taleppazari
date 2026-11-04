using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using TalepPazari.ApiTests.Support;
using TalepPazari.IntegrationTests.Support;
using TalepPazari.Modules.Example.Infrastructure;

namespace TalepPazari.ApiTests;

// Cursor pagination against real PostgreSQL. The fixture is per class (not per assembly), so the tests
// that need no database run, and pass, without Docker even when TALEPPAZARI_REQUIRE_DOCKER=true.
public sealed class PaginationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string ItemsRoute = "/api/v1/example/items";

    [Fact]
    public async Task Empty_list_returns_an_empty_page_without_a_next_cursor()
    {
        await using var api = await CreateApiAsync();

        var page = await GetPageAsync(api, $"{ItemsRoute}");

        Assert.Empty(page.Items);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task First_page_uses_the_default_limit_of_20()
    {
        await using var api = await CreateApiAsync();
        var created = await CreateItemsAsync(api, 21);

        var page = await GetPageAsync(api, ItemsRoute);

        Assert.Equal(created.Take(20), page.Items.Select(item => item.Id));
        Assert.NotNull(page.NextCursor);
    }

    [Fact]
    public async Task Maximum_limit_of_100_is_accepted()
    {
        await using var api = await CreateApiAsync();
        var created = await CreateItemsAsync(api, 3);

        var page = await GetPageAsync(api, $"{ItemsRoute}?limit=100");

        Assert.Equal(created, page.Items.Select(item => item.Id));
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task Pages_follow_the_cursor_in_ascending_Id_order_and_the_last_page_has_no_next_cursor()
    {
        await using var api = await CreateApiAsync();
        var created = await CreateItemsAsync(api, 5);

        var first = await GetPageAsync(api, $"{ItemsRoute}?limit=2");
        var second = await GetPageAsync(api, $"{ItemsRoute}?limit=2&cursor={first.NextCursor}");
        var last = await GetPageAsync(api, $"{ItemsRoute}?limit=2&cursor={second.NextCursor}");

        Assert.Equal(created[0..2], first.Items.Select(item => item.Id));
        Assert.Equal(created[2..4], second.Items.Select(item => item.Id));
        Assert.Equal(created[4..5], last.Items.Select(item => item.Id));
        Assert.NotNull(first.NextCursor);
        Assert.NotNull(second.NextCursor);
        Assert.Null(last.NextCursor);
        Assert.Equal("item-0", first.Items[0].Name);
    }

    [Fact]
    public async Task A_full_last_page_has_no_next_cursor()
    {
        await using var api = await CreateApiAsync();
        var created = await CreateItemsAsync(api, 4);

        var first = await GetPageAsync(api, $"{ItemsRoute}?limit=2");
        var last = await GetPageAsync(api, $"{ItemsRoute}?limit=2&cursor={first.NextCursor}");

        Assert.Equal(created[2..4], last.Items.Select(item => item.Id));
        Assert.Null(last.NextCursor);
    }

    [Fact]
    public async Task An_item_inserted_between_pages_causes_no_duplicate_and_no_skipped_item()
    {
        await using var api = await CreateApiAsync();
        var created = await CreateItemsAsync(api, 4);

        var first = await GetPageAsync(api, $"{ItemsRoute}?limit=2");
        created.AddRange(await CreateItemsAsync(api, 1, firstIndex: 4));
        var second = await GetPageAsync(api, $"{ItemsRoute}?limit=2&cursor={first.NextCursor}");
        var third = await GetPageAsync(api, $"{ItemsRoute}?limit=2&cursor={second.NextCursor}");

        var seen = first.Items.Concat(second.Items).Concat(third.Items).Select(item => item.Id).ToList();
        Assert.Equal(created, seen);
        Assert.Null(third.NextCursor);
    }

    // A fresh migrated database and a clock that moves 1 ms per item, so every UUID v7 has its own
    // millisecond (ids within one millisecond are not ordered; see docs/architecture/api-conventions.md).
    private async Task<TestApi> CreateApiAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var clock = new ManualTimeProvider();
        var api = TestApi.Create(connectionString, services => services.AddSingleton<TimeProvider>(clock));
        try
        {
            await ExampleModule.MigrateDatabaseAsync(api.Services, TestContext.Current.CancellationToken);
        }
        catch
        {
            // The caller's "await using" never sees the api, so dispose it here.
            await api.DisposeAsync();
            throw;
        }

        return api;
    }

    private static async Task<List<Guid>> CreateItemsAsync(TestApi api, int count, int firstIndex = 0)
    {
        var clock = (ManualTimeProvider)api.Services.GetRequiredService<TimeProvider>();
        var ids = new List<Guid>();
        for (var index = firstIndex; index < firstIndex + count; index++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(1));
            using var response = await api.Client.PostAsJsonAsync(
                ItemsRoute, new { name = $"item-{index}" }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var item = await response.Content.ReadFromJsonAsync<ItemDto>(TestContext.Current.CancellationToken);
            ids.Add(item!.Id);
        }

        return ids;
    }

    private static async Task<PageDto> GetPageAsync(TestApi api, string url)
    {
        using var response = await api.Client.GetAsync(url, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PageDto>(TestContext.Current.CancellationToken))!;
    }

    private sealed record ItemDto(Guid Id, string Name, DateTimeOffset CreatedAt);

    private sealed record PageDto(List<ItemDto> Items, string? NextCursor);
}

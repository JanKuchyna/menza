using System.Net.Http.Json;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using UTB.Minute.Contracts;
using Xunit;

namespace UTB.Minute.WebApi.Tests;

public class AspireIntegrationTests : IAsyncLifetime
{
    private DistributedApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.UTB_Minute_AppHost>();
        
        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        _client = _app.CreateHttpClient("utb-minute-webapi");

        var dbManagerClient = _app.CreateHttpClient("utb-minute-dbmanager");
        await dbManagerClient.PostAsync("/reset-db", null);
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
    }

    [Fact]
    public async Task Foods_Crud_Operations_Work()
    {
        var createDto = new CreateFoodDto("Testovací Řízek", "S bramborem", 120m);
        var createResponse = await _client.PostAsJsonAsync("/api/foods", createDto);
        createResponse.EnsureSuccessStatusCode();
        
        var createdFood = await createResponse.Content.ReadFromJsonAsync<FoodDto>();
        Assert.NotNull(createdFood);
        Assert.Equal("Testovací Řízek", createdFood.Name);

        var getResponse = await _client.GetAsync("/api/foods");
        var foods = await getResponse.Content.ReadFromJsonAsync<List<FoodDto>>();
        Assert.Contains(foods!, f => f.Id == createdFood.Id);

        var updateDto = new UpdateFoodDto("Změněný Řízek", "S kaší", 130m);
        var updateResponse = await _client.PutAsJsonAsync($"/api/foods/{createdFood.Id}", updateDto);
        updateResponse.EnsureSuccessStatusCode();

        var deleteResponse = await _client.DeleteAsync($"/api/foods/{createdFood.Id}");
        deleteResponse.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Order_Creation_Decreases_Available_Portions()
    {
        var menuResponse = await _client.GetFromJsonAsync<List<MenuItemDto>>("/api/menu");
        var menuItem = menuResponse!.First();
        int initialPortions = menuItem.AvailablePortions;

        var orderDto = new CreateOrderDto(menuItem.Id, "student_123");
        var orderResponse = await _client.PostAsJsonAsync("/api/orders", orderDto);
        orderResponse.EnsureSuccessStatusCode();
        
        var createdOrder = await orderResponse.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(createdOrder);
        Assert.Equal("Preparing", createdOrder.Status);

        var updatedMenuResponse = await _client.GetFromJsonAsync<List<MenuItemDto>>("/api/menu");
        var updatedMenuItem = updatedMenuResponse!.First(m => m.Id == menuItem.Id);
        
        Assert.Equal(initialPortions - 1, updatedMenuItem.AvailablePortions);
    }

    [Fact]
    public async Task Order_Status_Can_Be_Changed()
    {
        var menuResponse = await _client.GetFromJsonAsync<List<MenuItemDto>>("/api/menu");
        var menuItem = menuResponse!.First();
        
        var orderResponse = await _client.PostAsJsonAsync("/api/orders", new CreateOrderDto(menuItem.Id, "student_456"));
        var createdOrder = await orderResponse.Content.ReadFromJsonAsync<OrderDto>();

        var updateStatusDto = new UpdateOrderStatusDto("Ready");
        var patchResponse = await _client.PatchAsJsonAsync($"/api/orders/{createdOrder!.Id}/status", updateStatusDto);
        patchResponse.EnsureSuccessStatusCode();

        var getOrdersResponse = await _client.GetFromJsonAsync<List<OrderDto>>("/api/orders");
        var updatedOrder = getOrdersResponse!.First(o => o.Id == createdOrder.Id);
        
        Assert.Equal("Ready", updatedOrder.Status);
    }
}
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication;
using UTB.Minute.Db;
using UTB.Minute.Contracts;
using UTB.Minute.WebApi;

var builder = WebApplication.CreateBuilder(args);

// Aspire defaults (service discovery, telemetry, health checks)
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<MenzaContext>("database");

// SSE notification service
builder.Services.AddSingleton<OrderNotificationService>();

// JWT Bearer auth via Keycloak
var keycloakUrl = (builder.Configuration.GetConnectionString("keycloak") ?? "http://localhost:8080").TrimEnd('/');
var realm = builder.Configuration["Keycloak:Realm"] ?? "menza";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"{keycloakUrl}/realms/{realm}";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new()
        {
            ValidateAudience = false
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<IClaimsTransformation, KeycloakRolesTransformation>();

var app = builder.Build();
app.MapDefaultEndpoints();

app.UseAuthentication();
app.UseAuthorization();

var api = app.MapGroup("/api");

// ── Food Endpoints ────────────────────────────────────────────────────────────
var foods = api.MapGroup("/foods");

foods.MapGet("/", async (MenzaContext db) =>
    TypedResults.Ok(await db.Foods
        .Select(f => new FoodDto(f.Id, f.Name, f.Description, f.Price, f.IsActive))
        .ToListAsync()));

foods.MapPost("/", async (CreateFoodDto dto, MenzaContext db) =>
{
    var food = new Food { Name = dto.Name, Description = dto.Description, Price = dto.Price, IsActive = true };
    db.Foods.Add(food);
    await db.SaveChangesAsync();

    var resultDto = new FoodDto(food.Id, food.Name, food.Description, food.Price, food.IsActive);
    return TypedResults.Created($"/api/foods/{food.Id}", resultDto);
}).RequireAuthorization(policy => policy.RequireRole("admin"));

foods.MapPut("/{id}", async (int id, UpdateFoodDto dto, MenzaContext db) =>
{
    var food = await db.Foods.FindAsync(id);
    if (food is null) return Results.NotFound();

    food.Name = dto.Name;
    food.Description = dto.Description;
    food.Price = dto.Price;
    await db.SaveChangesAsync();

    return TypedResults.NoContent();
}).RequireAuthorization(policy => policy.RequireRole("admin"));

foods.MapDelete("/{id}", async (int id, MenzaContext db) =>
{
    var food = await db.Foods.FindAsync(id);
    if (food is null) return Results.NotFound();

    food.IsActive = false;
    await db.SaveChangesAsync();

    return TypedResults.NoContent();
}).RequireAuthorization(policy => policy.RequireRole("admin"));

foods.MapPost("/{id}/activate", async (int id, MenzaContext db) =>
{
    var food = await db.Foods.FindAsync(id);
    if (food is null) return Results.NotFound();

    food.IsActive = true;
    await db.SaveChangesAsync();

    return TypedResults.NoContent();
}).RequireAuthorization(policy => policy.RequireRole("admin"));

// ── Menu Endpoints ────────────────────────────────────────────────────────────
var menu = api.MapGroup("/menu");

menu.MapGet("/", async (MenzaContext db) =>
    TypedResults.Ok(await db.MenuItems
        .Include(m => m.Food)
        .Select(m => new MenuItemDto(
            m.Id, m.Date,
            new FoodDto(m.Food.Id, m.Food.Name, m.Food.Description, m.Food.Price, m.Food.IsActive),
            m.AvailablePortions))
        .ToListAsync()));

menu.MapPost("/", async (CreateMenuItemDto dto, MenzaContext db) =>
{
    var food = await db.Foods.FindAsync(dto.FoodId);
    if (food is null) return Results.NotFound("Food not found");

    var menuItem = new MenuItem { Date = dto.Date, FoodId = dto.FoodId, AvailablePortions = dto.AvailablePortions };
    db.MenuItems.Add(menuItem);
    await db.SaveChangesAsync();

    var resultDto = new MenuItemDto(menuItem.Id, menuItem.Date,
        new FoodDto(food.Id, food.Name, food.Description, food.Price, food.IsActive),
        menuItem.AvailablePortions);

    return TypedResults.Created($"/api/menu/{menuItem.Id}", resultDto);
}).RequireAuthorization(policy => policy.RequireRole("admin"));

menu.MapPut("/{id}", async (int id, UpdateMenuItemDto dto, MenzaContext db) =>
{
    var menuItem = await db.MenuItems.FindAsync(id);
    if (menuItem is null) return Results.NotFound();

    menuItem.Date = dto.Date;
    menuItem.FoodId = dto.FoodId;
    menuItem.AvailablePortions = dto.AvailablePortions;
    await db.SaveChangesAsync();

    return TypedResults.NoContent();
}).RequireAuthorization(policy => policy.RequireRole("admin"));

menu.MapDelete("/{id}", async (int id, MenzaContext db) =>
{
    var menuItem = await db.MenuItems.FindAsync(id);
    if (menuItem is null) return Results.NotFound();

    db.MenuItems.Remove(menuItem);
    await db.SaveChangesAsync();

    return TypedResults.NoContent();
}).RequireAuthorization(policy => policy.RequireRole("admin"));

// ── Orders Endpoints ──────────────────────────────────────────────────────────
var orders = api.MapGroup("/orders");

orders.MapGet("/", async (MenzaContext db) =>
    TypedResults.Ok(await db.Orders
        .Include(o => o.MenuItem).ThenInclude(m => m.Food)
        .Where(o => o.Status != OrderStatus.Completed)
        .Select(o => new OrderDto(
            o.Id,
            new MenuItemDto(o.MenuItem.Id, o.MenuItem.Date, new FoodDto(o.MenuItem.Food.Id, o.MenuItem.Food.Name, o.MenuItem.Food.Description, o.MenuItem.Food.Price, o.MenuItem.Food.IsActive), o.MenuItem.AvailablePortions),
            o.StudentId, o.Status.ToString(), o.CreatedAt))
        .ToListAsync())
).RequireAuthorization(policy => policy.RequireRole("cook"));

orders.MapPost("/", async (CreateOrderDto dto, MenzaContext db, OrderNotificationService notifier) =>
{
    var menuItem = await db.MenuItems.Include(m => m.Food).FirstOrDefaultAsync(m => m.Id == dto.MenuItemId);
    if (menuItem is null) return Results.NotFound("Menu item not found.");
    if (menuItem.AvailablePortions <= 0) return Results.BadRequest("This meal is sold out.");

    menuItem.AvailablePortions--;
    var order = new Order { MenuItemId = menuItem.Id, StudentId = dto.StudentId, Status = OrderStatus.Preparing };
    db.Orders.Add(order);

    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        return Results.Conflict("Someone else just ordered the last portion. Please try again.");
    }

    await notifier.NotifyAsync("order_changed");

    var resultDto = new OrderDto(order.Id,
        new MenuItemDto(menuItem.Id, menuItem.Date, new FoodDto(menuItem.Food.Id, menuItem.Food.Name, menuItem.Food.Description, menuItem.Food.Price, menuItem.Food.IsActive), menuItem.AvailablePortions),
        order.StudentId, order.Status.ToString(), order.CreatedAt);

    return TypedResults.Created($"/api/orders/{order.Id}", resultDto);
});

orders.MapPatch("/{id}/status", async (int id, UpdateOrderStatusDto dto, MenzaContext db, OrderNotificationService notifier) =>
{
    var order = await db.Orders.FindAsync(id);
    if (order is null) return Results.NotFound();

    if (!Enum.TryParse<OrderStatus>(dto.Status, out var newStatus))
        return Results.BadRequest("Invalid status.");

    if (order.Status == OrderStatus.Cancelled || order.Status == OrderStatus.Completed)
        return Results.BadRequest("Cannot change status of a cancelled or completed order.");

    var allowed = order.Status switch
    {
        OrderStatus.Preparing => new[] { OrderStatus.Ready, OrderStatus.Cancelled },
        OrderStatus.Ready => new[] { OrderStatus.Completed, OrderStatus.Cancelled },
        _ => Array.Empty<OrderStatus>()
    };

    if (!allowed.Contains(newStatus))
        return Results.BadRequest($"Invalid state transition from {order.Status} to {newStatus}.");

    order.Status = newStatus;
    await db.SaveChangesAsync();

    await notifier.NotifyAsync("order_changed");

    return TypedResults.NoContent();
}).RequireAuthorization(policy => policy.RequireRole("cook"));

// ── SSE Endpoint (public – no auth required) ──────────────────────────────────
orders.MapGet("/events", async (OrderNotificationService notifier, HttpContext ctx, CancellationToken ct) =>
{
    ctx.Response.Headers.ContentType = "text/event-stream";
    ctx.Response.Headers.CacheControl = "no-cache";
    ctx.Response.Headers.Connection = "keep-alive";

    var channel = notifier.Subscribe();
    try
    {
        await foreach (var msg in channel.Reader.ReadAllAsync(ct))
        {
            await ctx.Response.WriteAsync($"data: {msg}\n\n", ct);
            await ctx.Response.Body.FlushAsync(ct);
        }
    }
    catch (OperationCanceledException) { }
    finally
    {
        notifier.Unsubscribe(channel);
    }
}).AllowAnonymous();

app.Run();
public partial class Program { }

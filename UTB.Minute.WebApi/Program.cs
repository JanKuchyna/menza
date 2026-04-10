using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;
using UTB.Minute.Db;
using UTB.Minute.Contracts;

var builder = WebApplication.CreateBuilder(args);

// connecting to database
builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<MenzaContext>("database");

var app = builder.Build();
app.MapDefaultEndpoints();

var api = app.MapGroup("/api");

// Food Endpoints
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
});

foods.MapPut("/{id}", async (int id, UpdateFoodDto dto, MenzaContext db) =>
{
    var food = await db.Foods.FindAsync(id);
    if (food is null) return Results.NotFound();

    food.Name = dto.Name;
    food.Description = dto.Description;
    food.Price = dto.Price;
    await db.SaveChangesAsync();
    
    return TypedResults.NoContent();
});

foods.MapDelete("/{id}", async (int id, MenzaContext db) =>
{
    var food = await db.Foods.FindAsync(id);
    if (food is null) return Results.NotFound();

    food.IsActive = false;
    await db.SaveChangesAsync();
    
    return TypedResults.NoContent();
});

// menu endpoiunts
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
});

menu.MapPut("/{id}", async (int id, UpdateMenuItemDto dto, MenzaContext db) =>
{
    var menuItem = await db.MenuItems.FindAsync(id);
    if (menuItem is null) return Results.NotFound();

    menuItem.Date = dto.Date;
    menuItem.FoodId = dto.FoodId;
    menuItem.AvailablePortions = dto.AvailablePortions;
    await db.SaveChangesAsync();
    
    return TypedResults.NoContent();
});

menu.MapDelete("/{id}", async (int id, MenzaContext db) =>
{
    var menuItem = await db.MenuItems.FindAsync(id);
    if (menuItem is null) return Results.NotFound();

    db.MenuItems.Remove(menuItem);
    await db.SaveChangesAsync();
    
    return TypedResults.NoContent();
});

// Orders endpoints
var orders = api.MapGroup("/orders");

orders.MapGet("/", async (MenzaContext db) => 
    TypedResults.Ok(await db.Orders
        .Include(o => o.MenuItem).ThenInclude(m => m.Food)
        .Select(o => new OrderDto(
            o.Id, 
            new MenuItemDto(o.MenuItem.Id, o.MenuItem.Date, new FoodDto(o.MenuItem.Food.Id, o.MenuItem.Food.Name, o.MenuItem.Food.Description, o.MenuItem.Food.Price, o.MenuItem.Food.IsActive), o.MenuItem.AvailablePortions),
            o.StudentId, o.Status.ToString(), o.CreatedAt))
        .ToListAsync()));

orders.MapPost("/", async (CreateOrderDto dto, MenzaContext db) =>
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

    var resultDto = new OrderDto(order.Id, 
        new MenuItemDto(menuItem.Id, menuItem.Date, new FoodDto(menuItem.Food.Id, menuItem.Food.Name, menuItem.Food.Description, menuItem.Food.Price, menuItem.Food.IsActive), menuItem.AvailablePortions),
        order.StudentId, order.Status.ToString(), order.CreatedAt);

    return TypedResults.Created($"/api/orders/{order.Id}", resultDto);
});

orders.MapPatch("/{id}/status", async (int id, UpdateOrderStatusDto dto, MenzaContext db) =>
{
    var order = await db.Orders.FindAsync(id);
    if (order is null) return Results.NotFound();

    if (Enum.TryParse<OrderStatus>(dto.Status, out var newStatus))
    {
        order.Status = newStatus;
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }
    
    return Results.BadRequest("Invalid status.");
});

app.Run();
public partial class Program { }
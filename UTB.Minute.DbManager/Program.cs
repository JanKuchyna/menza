using Microsoft.EntityFrameworkCore;
using UTB.Minute.Db;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<MenzaContext>("database");

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapPost("/reset-db", async (MenzaContext context) =>
{
    await context.Database.EnsureDeletedAsync();
    await context.Database.EnsureCreatedAsync();

    var svickova = new Food { Name = "Svíčková", Description = "Hovězí svíčková na smetaně s houskovým knedlíkem", Price = 89m };
    var rizek = new Food { Name = "Řízek", Description = "Smažený vepřový řízek s bramborovým salátem", Price = 79m };
    var gulasova = new Food { Name = "Gulášová polévka", Description = "Gulášová polévka s chlebem", Price = 35m };

    context.Foods.AddRange(svickova, rizek, gulasova);
    await context.SaveChangesAsync();

    var today = DateOnly.FromDateTime(DateTime.Today);

    context.MenuItems.AddRange(
        new MenuItem { Date = today, Food = svickova, AvailablePortions = 20 },
        new MenuItem { Date = today, Food = rizek, AvailablePortions = 15 },
        new MenuItem { Date = today.AddDays(1), Food = gulasova, AvailablePortions = 30 }
    );

    await context.SaveChangesAsync();
});

app.UseHttpsRedirection();

app.Run();

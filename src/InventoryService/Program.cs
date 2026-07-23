using InventoryService;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Inventory")));
builder.Services.AddProblemDetails();

var app = builder.Build();

// Component tests supply their own store and their own data.
if (!app.Environment.IsEnvironment("Testing"))
{
    await app.Services.MigrateAndSeedAsync();
}

app.MapStockEndpoints();

app.Run();

// Exposed so WebApplicationFactory<Program> can host this service in component
// and provider-verification tests.
public partial class Program;

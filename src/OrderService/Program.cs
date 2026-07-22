using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService;
using OrderService.Clients;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Orders")));

builder.Services.AddHttpClient<InventoryClient>(client =>
        client.BaseAddress = new Uri(builder.Configuration["Services:Inventory"]!))
    .AddStandardResilienceHandler();

builder.Services.AddHttpClient<PaymentClient>(client =>
        client.BaseAddress = new Uri(builder.Configuration["Services:Payments"]!))
    .AddStandardResilienceHandler();

builder.Services.AddMassTransit(bus =>
{
    bus.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"]!, host =>
        {
            host.Username(builder.Configuration["RabbitMq:Username"]!);
            host.Password(builder.Configuration["RabbitMq:Password"]!);
        });

        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<OrderPlacementService>();
builder.Services.AddProblemDetails();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<OrderDbContext>().Database.MigrateAsync();
}

app.MapOrderEndpoints();

app.Run();

// Exposed so WebApplicationFactory<Program> can host this service in component tests.
public partial class Program;

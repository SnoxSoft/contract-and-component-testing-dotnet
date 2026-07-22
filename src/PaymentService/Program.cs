using Microsoft.EntityFrameworkCore;
using PaymentService;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PaymentDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Payments")));
builder.Services.AddProblemDetails();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Database.MigrateAsync();
}

app.MapPaymentEndpoints();

app.Run();

// Exposed so WebApplicationFactory<Program> can host this service in component
// and provider-verification tests.
public partial class Program;

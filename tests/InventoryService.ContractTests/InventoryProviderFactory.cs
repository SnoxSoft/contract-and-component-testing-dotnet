using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace InventoryService.ContractTests;

/// <summary>
/// Hosts InventoryService on a real Kestrel port so the Pact verifier, which runs
/// out of process, can reach it. Exposes a provider-state endpoint the verifier
/// calls before each interaction to put the database into the declared state.
/// </summary>
public class InventoryProviderFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private NpgsqlConnection _connection = null!;
    private Respawner _respawner = null!;

    public Uri ServerUri { get; private set; } = null!;

    public Uri ProviderStateUri => new(ServerUri, "/provider-states");

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // Port 0 asks the OS for a free port. The parameterless overload binds the default
        // http://127.0.0.1:5000, which collides when several provider suites run in parallel.
        UseKestrel(0);
        StartServer();
        ServerUri = CreateClient().BaseAddress!;

        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
            await db.Database.MigrateAsync();
        }

        _connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await _connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Respawn.Graph.Table("__EFMigrationsHistory")]
        });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Inventory", _postgres.GetConnectionString());

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IStartupFilter>(new ProviderStateStartupFilter(this));
        });
    }

    /// <summary>Runs before every interaction. Each state is the world the consumer described.</summary>
    public async Task SetUpStateAsync(string state)
    {
        await _respawner.ResetAsync(_connection);

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        switch (state)
        {
            case "stock exists for SKU-COFFEE":
                db.StockItems.Add(new StockItem { Sku = "SKU-COFFEE", AvailableQuantity = 42, UnitPriceCents = 1_250 });
                await db.SaveChangesAsync();
                break;

            case "no stock exists for SKU-NOPE":
                break;

            default:
                throw new InvalidOperationException($"No handler for provider state '{state}'.");
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    private sealed class ProviderStateStartupFilter(InventoryProviderFactory factory) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Map("/provider-states", branch => branch.Run(async context =>
            {
                var body = await context.Request.ReadFromJsonAsync<ProviderStateRequest>();

                if (body?.Action == "setup" && body.State is not null)
                {
                    await factory.SetUpStateAsync(body.State);
                }

                context.Response.StatusCode = StatusCodes.Status200OK;
            }));

            next(app);
        };
    }

    private sealed record ProviderStateRequest(string? Action, string? State);
}

using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;
using WireMock.Server;

namespace OrderService.ComponentTests;

public class OrderApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private NpgsqlConnection _connection = null!;
    private Respawner _respawner = null!;

    /// <summary>Stands in for InventoryService. Tests program it per scenario.</summary>
    public WireMockServer Inventory { get; } = WireMockServer.Start();

    /// <summary>Stands in for PaymentService.</summary>
    public WireMockServer Payments { get; } = WireMockServer.Start();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
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

    public async ValueTask ResetAsync()
    {
        await _respawner.ResetAsync(_connection);
        Inventory.Reset();
        Payments.Reset();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // OrderService reads every collaborator address from configuration, so the
        // whole graph can be redirected without touching the service collection.
        builder.UseSetting("ConnectionStrings:Orders", _postgres.GetConnectionString());
        builder.UseSetting("Services:Inventory", Inventory.Url!);
        builder.UseSetting("Services:Payments", Payments.Url!);

        builder.ConfigureServices(services =>
        {
            // The broker is the one collaborator with no configurable in-process form,
            // so MassTransit's registrations are replaced with its in-memory harness.
            var massTransit = services
                .Where(d => IsMassTransit(d.ServiceType) || IsMassTransit(d.ImplementationType))
                .ToList();

            foreach (var descriptor in massTransit)
            {
                services.Remove(descriptor);
            }

            services.AddMassTransitTestHarness();
        });
    }

    private static bool IsMassTransit(Type? type) =>
        type?.Assembly.GetName().Name?.StartsWith("MassTransit", StringComparison.Ordinal) == true;

    public override async ValueTask DisposeAsync()
    {
        Inventory.Stop();
        Payments.Stop();
        await _connection.DisposeAsync();
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition(nameof(OrderCollection))]
public class OrderCollection : ICollectionFixture<OrderApiFactory>;

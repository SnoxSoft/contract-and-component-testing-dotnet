using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Respawn;
using Testcontainers.PostgreSql;

namespace PaymentService.ComponentTests;

public class PaymentApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Same image as docker-compose, so tests and local runs agree on the engine version.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    private NpgsqlConnection _connection = null!;
    private Respawner _respawner = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // Touching Services builds the host, which needs the container's connection string,
        // so the container has to be running before this point.
        await using (var scope = Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
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

    /// <summary>Deletes all rows while leaving the schema and migration history intact.</summary>
    public async ValueTask ResetAsync() => await _respawner.ResetAsync(_connection);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // AddDbContext registers the connection-string delegate as IDbContextOptionsConfiguration.
            // Leaving it in place would point the context at the developer's local PostgreSQL.
            services.RemoveAll<IDbContextOptionsConfiguration<PaymentDbContext>>();
            services.RemoveAll<DbContextOptions<PaymentDbContext>>();
            services.RemoveAll<PaymentDbContext>();

            services.AddDbContext<PaymentDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}

[CollectionDefinition(nameof(PaymentCollection))]
public class PaymentCollection : ICollectionFixture<PaymentApiFactory>;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace PaymentService.ContractTests;

/// <summary>
/// Hosts PaymentService on a real Kestrel port for the out-of-process verifier.
/// Neither interaction in the contract declares a provider state: the outcome of a
/// payment depends only on the request, so there is no world to set up first.
/// </summary>
public class PaymentProviderFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Uri ServerUri { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        UseKestrel();
        StartServer();
        ServerUri = CreateClient().BaseAddress!;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Payments", _postgres.GetConnectionString());
    }

    public override async ValueTask DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}

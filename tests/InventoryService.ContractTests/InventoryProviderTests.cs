using PactNet;
using PactNet.Infrastructure.Outputters;
using PactNet.Verifier;

namespace InventoryService.ContractTests;

public class InventoryProviderTests(InventoryProviderFactory factory) : IClassFixture<InventoryProviderFactory>
{
    private static readonly string PactPath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "pacts", "OrderService-InventoryService.json");

    [Fact]
    public void Honours_the_contract_published_by_OrderService()
    {
        using var verifier = new PactVerifier("InventoryService", new PactVerifierConfig
        {
            Outputters = [new TestOutput()],
            LogLevel = PactLogLevel.Warn
        });

        verifier
            .WithHttpEndpoint(factory.ServerUri)
            .WithFileSource(new FileInfo(PactPath))
            .WithProviderStateUrl(factory.ProviderStateUri)
            .Verify();
    }

    // Routes the verifier's report into the test output, so a mismatch is readable
    // in the test explorer and in CI logs instead of only in a generic exception.
    private sealed class TestOutput : IOutput
    {
        public void WriteLine(string line) => TestContext.Current.TestOutputHelper?.WriteLine(line);
    }
}

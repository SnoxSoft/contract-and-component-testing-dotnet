using PactNet;
using PactNet.Infrastructure.Outputters;
using PactNet.Verifier;

namespace PaymentService.ContractTests;

public class PaymentProviderTests(PaymentProviderFactory factory) : IClassFixture<PaymentProviderFactory>
{
    private static readonly string PactPath = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "pacts", "OrderService-PaymentService.json");

    [Fact]
    public void Honours_the_contract_published_by_OrderService()
    {
        using var verifier = new PactVerifier("PaymentService", new PactVerifierConfig
        {
            Outputters = [new TestOutput()],
            LogLevel = PactLogLevel.Warn
        });

        verifier
            .WithHttpEndpoint(factory.ServerUri)
            .WithFileSource(new FileInfo(PactPath))
            .Verify();
    }

    private sealed class TestOutput : IOutput
    {
        public void WriteLine(string line) => TestContext.Current.TestOutputHelper?.WriteLine(line);
    }
}

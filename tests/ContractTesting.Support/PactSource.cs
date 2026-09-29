using PactNet.Verifier;

namespace ContractTesting.Support;

/// <summary>
/// Chooses where contracts come from. Locally that is the committed file, which needs
/// no infrastructure. When PACT_BROKER_BASE_URL is set, which is how CI runs, contracts
/// come from the broker and the verification result is published back, so the broker
/// can answer can-i-deploy for this provider version.
/// </summary>
public static class PactSource
{
    public static IPactVerifierSource FromBrokerOrFile(
        this IPactVerifier verifier, string pactFileName)
    {
        var brokerUrl = Environment.GetEnvironmentVariable("PACT_BROKER_BASE_URL");

        if (string.IsNullOrWhiteSpace(brokerUrl))
        {
            var path = Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "..", "pacts", pactFileName);

            return verifier.WithFileSource(new FileInfo(path));
        }

        var version = Environment.GetEnvironmentVariable("GIT_COMMIT") ?? "dev";
        var branch = Environment.GetEnvironmentVariable("GIT_BRANCH") ?? "local";

        return verifier.WithPactBrokerSource(new Uri(brokerUrl), options =>
        {
            options
                .BasicAuthentication(
                    Environment.GetEnvironmentVariable("PACT_BROKER_USERNAME") ?? "pact",
                    Environment.GetEnvironmentVariable("PACT_BROKER_PASSWORD") ?? "pact")
                // Verify what is on the consumer's main branch, plus whatever is on the
                // branch being built. The first keeps a provider honest about released
                // consumers; the second lets a consumer's change be verified before it
                // merges, which is the point of publishing from a feature branch.
                .ConsumerVersionSelectors(
                    new ConsumerVersionSelector { MainBranch = true },
                    new ConsumerVersionSelector { Branch = branch })
                .PublishResults(version, results => results.ProviderBranch(branch));
        });
    }
}

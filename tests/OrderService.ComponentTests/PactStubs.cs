using System.Text.Json;
using System.Text.Json.Nodes;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace OrderService.ComponentTests;

/// <summary>
/// Builds WireMock stubs from the recorded pact files instead of from hand-written
/// literals, so a component-test stub cannot describe a response the provider has
/// never agreed to produce.
/// </summary>
public static class PactStubs
{
    private static readonly Dictionary<string, JsonNode> Cache = new();

    /// <summary>
    /// Stubs <paramref name="server"/> with the interaction recorded under
    /// <paramref name="description"/>. Field names, status, headers and content type
    /// all come from the pact; <paramref name="overrides"/> replaces example values so
    /// a test can set up the scenario it needs.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when an override names a field the pact does not contain. That means the
    /// stub and the contract have drifted apart, which is the failure this class exists
    /// to make loud.
    /// </exception>
    public static void StubFromPact(
        this WireMockServer server,
        string pactFile,
        string description,
        string? path = null,
        IReadOnlyDictionary<string, object?>? overrides = null)
    {
        var interaction = FindInteraction(pactFile, description);

        var request = interaction["request"]
            ?? throw new InvalidOperationException($"Interaction '{description}' has no request.");
        var response = interaction["response"]
            ?? throw new InvalidOperationException($"Interaction '{description}' has no response.");

        var method = request["method"]!.GetValue<string>();
        var requestPath = path ?? request["path"]!.GetValue<string>();
        var status = response["status"]!.GetValue<int>();

        var builder = Response.Create().WithStatusCode(status);

        foreach (var header in response["headers"]?.AsObject() ?? [])
        {
            var values = header.Value!.AsArray().Select(v => v!.GetValue<string>()).ToArray();
            builder = builder.WithHeader(header.Key, values);
        }

        var body = response["body"]?["content"]?.DeepClone();

        if (body is not null)
        {
            ApplyOverrides(body.AsObject(), overrides, description);
            builder = builder.WithBody(body.ToJsonString());
        }

        server
            .Given(Request.Create().WithPath(requestPath).UsingMethod(method))
            .RespondWith(builder);
    }

    private static void ApplyOverrides(
        JsonObject body, IReadOnlyDictionary<string, object?>? overrides, string description)
    {
        if (overrides is null)
        {
            return;
        }

        foreach (var (name, value) in overrides)
        {
            if (!body.ContainsKey(name))
            {
                throw new InvalidOperationException(
                    $"The pact interaction '{description}' has no field '{name}'. " +
                    $"It records: {string.Join(", ", body.Select(p => p.Key))}. " +
                    "Either the provider renamed it and the contract was regenerated, " +
                    "or this stub is describing a response nobody agreed to.");
            }

            body[name] = value is null ? null : JsonSerializer.SerializeToNode(value);
        }
    }

    private static JsonNode FindInteraction(string pactFile, string description)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(pactFile, out var pact))
            {
                // Read from the build output, not from pacts/ directly: the contract test
                // projects rewrite those files and can run at the same time as this one.
                var path = Path.Combine(AppContext.BaseDirectory, "pacts", pactFile);

                pact = JsonNode.Parse(File.ReadAllText(path))
                       ?? throw new InvalidOperationException($"{pactFile} is empty.");

                Cache[pactFile] = pact;
            }

            var interactions = pact["interactions"]?.AsArray()
                ?? throw new InvalidOperationException($"{pactFile} has no interactions.");

            return interactions.FirstOrDefault(i => i?["description"]?.GetValue<string>() == description)
                   ?? throw new InvalidOperationException(
                       $"{pactFile} has no interaction '{description}'. It records: " +
                       string.Join(", ", interactions.Select(i => $"'{i?["description"]}'")));
        }
    }
}

using Shouldly;
using System.Net;
using System.Net.Http.Json;

namespace PaymentService.ComponentTests;

[Collection(nameof(PaymentCollection))]
public class PaymentEndpointTests(PaymentApiFactory factory) : IAsyncLifetime
{
    // The container is shared by every test in the collection, so each test starts
    // from an empty database rather than inheriting the previous test's rows.
    public ValueTask InitializeAsync() => factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    
    [Fact]
    public async Task Authorises_a_payment_within_the_credit_limit()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var orderId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync(
            "/payments", new AuthorisePaymentRequest(orderId, 3_048, "EUR"), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>(ct);

        payment.ShouldNotBeNull();
        payment.Status.ShouldBe(PaymentStatus.Authorized);
        payment.OrderId.ShouldBe(orderId);
        payment.AmountCents.ShouldBe(3_048);
        payment.Currency.ShouldBe("EUR");
        payment.PaymentId.ShouldNotBe(Guid.Empty);

        response.Headers.Location!.ToString().ShouldBe($"/payments/{payment.PaymentId}");
    }

    [Fact]
    public async Task Authorises_a_payment_at_exactly_the_credit_limit()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/payments", new AuthorisePaymentRequest(Guid.NewGuid(), 50_000, "EUR"), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>(ct);
        payment.ShouldNotBeNull();
        payment.Status.ShouldBe(PaymentStatus.Authorized);
    }

    [Fact]
    public async Task Declines_a_payment_one_cent_over_the_credit_limit()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/payments", new AuthorisePaymentRequest(Guid.NewGuid(), 50_001, "EUR"), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var payment = await response.Content.ReadFromJsonAsync<PaymentResponse>(ct);
        payment.ShouldNotBeNull();
        payment.Status.ShouldBe(PaymentStatus.Declined);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Rejects_a_non_positive_amount(int amountCents)
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/payments", new AuthorisePaymentRequest(Guid.NewGuid(), amountCents, "EUR"), ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var problem = await response.Content.ReadAsStringAsync(ct);
        problem.ShouldContain("Invalid amount");
    }

    [Fact]
    public async Task Persists_a_declined_payment_so_it_can_be_read_back()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();

        var declineResponse = await client.PostAsJsonAsync(
            "/payments", new AuthorisePaymentRequest(Guid.NewGuid(), 89_900, "EUR"), ct);

        var declined = await declineResponse.Content.ReadFromJsonAsync<PaymentResponse>(ct);
        declined.ShouldNotBeNull();

        var readBack = await client.GetAsync($"/payments/{declined.PaymentId}", ct);

        readBack.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payment = await readBack.Content.ReadFromJsonAsync<PaymentResponse>(ct);
        payment.ShouldNotBeNull();
        payment.PaymentId.ShouldBe(declined.PaymentId);
        payment.Status.ShouldBe(PaymentStatus.Declined);
        payment.AmountCents.ShouldBe(89_900);
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_payment()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/payments/{Guid.NewGuid()}", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var problem = await response.Content.ReadAsStringAsync(ct);
        problem.ShouldContain("Unknown payment");
    }

    [Fact]
    public async Task Rejects_an_id_that_is_not_a_guid()
    {
        var ct = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();

        var response = await client.GetAsync("/payments/not-a-guid", ct);

        // The :guid route constraint means no endpoint matches at all.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
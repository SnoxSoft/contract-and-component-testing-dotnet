using System.Net;
using OrderService.Clients;
using PactNet;
using PactNet.Matchers;
using Shouldly;

namespace OrderService.ContractTests;

public class PaymentClientContractTests
{
    private static readonly Guid OrderId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly IPactBuilderV4 _pact = Pact
        .V4("OrderService", "PaymentService", new PactConfig
        {
            PactDir = Path.Combine("..", "..", "..", "..", "..", "pacts")
        })
        .WithHttpInteractions();

    [Fact]
    public async Task Authorises_a_payment_within_the_credit_limit()
    {
        _pact
            .UponReceiving("a payment request within the credit limit")
                .WithRequest(HttpMethod.Post, "/payments")
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                .WithJsonBody(new { orderId = OrderId, amountCents = 3_048, currency = "EUR" })
            .WillRespond()
                .WithStatus(HttpStatusCode.Created)
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                .WithJsonBody(new
                {
                    paymentId = Match.Type(Guid.Parse("22222222-2222-2222-2222-222222222222")),
                    orderId = Match.Type(OrderId),
                    amountCents = Match.Integer(3_048),
                    currency = Match.Type("EUR"),
                    // The client branches on this exact string, so it is genuinely part of the deal.
                    status = "Authorized"
                });

        await _pact.VerifyAsync(async ctx =>
        {
            var client = new PaymentClient(new HttpClient { BaseAddress = ctx.MockServerUri });

            var result = await client.AuthoriseAsync(new AuthorisePaymentRequest(OrderId, 3_048, "EUR"));

            result.Status.ShouldBe(PaymentClient.Authorized);
            result.OrderId.ShouldBe(OrderId);
            result.PaymentId.ShouldNotBe(Guid.Empty);
        });
    }

    [Fact]
    public async Task Reads_the_decline_body_rather_than_treating_422_as_an_error()
    {
        _pact
            .UponReceiving("a payment request over the credit limit")
                .WithRequest(HttpMethod.Post, "/payments")
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                .WithJsonBody(new { orderId = OrderId, amountCents = 89_900, currency = "EUR" })
            .WillRespond()
                .WithStatus(HttpStatusCode.UnprocessableEntity)
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                .WithJsonBody(new
                {
                    paymentId = Match.Type(Guid.Parse("22222222-2222-2222-2222-222222222222")),
                    orderId = Match.Type(OrderId),
                    amountCents = Match.Integer(89_900),
                    currency = Match.Type("EUR"),
                    status = "Declined"
                });

        await _pact.VerifyAsync(async ctx =>
        {
            var client = new PaymentClient(new HttpClient { BaseAddress = ctx.MockServerUri });

            var result = await client.AuthoriseAsync(new AuthorisePaymentRequest(OrderId, 89_900, "EUR"));

            result.Status.ShouldBe(PaymentClient.Declined);
        });
    }
}

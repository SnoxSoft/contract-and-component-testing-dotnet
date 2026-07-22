using System.Net;
using System.Net.Http.Json;

namespace OrderService.Clients;

public record AuthorisePaymentRequest(Guid OrderId, int AmountCents, string Currency);

public record PaymentResult(Guid PaymentId, Guid OrderId, int AmountCents, string Currency, string Status);

public class PaymentClient(HttpClient httpClient)
{
    public const string Authorized = "Authorized";
    public const string Declined = "Declined";

    public async Task<PaymentResult> AuthoriseAsync(AuthorisePaymentRequest request, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/payments", request, ct);

        // A decline is a normal business outcome, not a transport failure, so it
        // carries a body we care about just as much as the success case.
        if (response.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.UnprocessableEntity))
        {
            response.EnsureSuccessStatusCode();
            throw new InvalidOperationException(
                $"Payment service returned an unexpected status {(int)response.StatusCode}.");
        }

        return await response.Content.ReadFromJsonAsync<PaymentResult>(ct)
               ?? throw new InvalidOperationException("Payment service returned an empty body.");
    }
}

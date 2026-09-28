namespace OrderService.ComponentTests;

/// <summary>
/// Collaborator responses for component tests, built from the recorded contracts.
/// Field names, status codes and content types come from the pact files; only the
/// values vary, so a stub here cannot describe a response the provider has never
/// agreed to produce.
/// </summary>
public static class StubExtensions
{
    private const string InventoryPact = "OrderService-InventoryService.json";
    private const string PaymentPact = "OrderService-PaymentService.json";

    private const string StockFound = "a request for the stock level of a known SKU";
    private const string StockMissing = "a request for the stock level of an unknown SKU";
    private const string PaymentAuthorised = "a payment request within the credit limit";
    private const string PaymentDeclined = "a payment request over the credit limit";

    public static void StubStock(
        this OrderApiFactory factory, string sku, int availableQuantity, int unitPriceCents) =>
        factory.Inventory.StubFromPact(InventoryPact, StockFound,
            path: $"/stock/{sku}",
            overrides: new Dictionary<string, object?>
            {
                ["sku"] = sku,
                ["availableQuantity"] = availableQuantity,
                ["unitPriceCents"] = unitPriceCents
            });

    public static void StubStockNotFound(this OrderApiFactory factory, string sku) =>
        factory.Inventory.StubFromPact(InventoryPact, StockMissing, path: $"/stock/{sku}");

    public static void StubPaymentAuthorised(this OrderApiFactory factory) =>
        factory.Payments.StubFromPact(PaymentPact, PaymentAuthorised,
            overrides: new Dictionary<string, object?>
            {
                ["paymentId"] = Guid.NewGuid(),
                ["orderId"] = Guid.NewGuid()
            });

    public static void StubPaymentDeclined(this OrderApiFactory factory) =>
        factory.Payments.StubFromPact(PaymentPact, PaymentDeclined,
            overrides: new Dictionary<string, object?>
            {
                ["paymentId"] = Guid.NewGuid(),
                ["orderId"] = Guid.NewGuid()
            });
}

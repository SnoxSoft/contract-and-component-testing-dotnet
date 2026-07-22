using System.Net;
using System.Net.Http.Json;

namespace OrderService.Clients;

public record StockItem(string Sku, int AvailableQuantity, int UnitPriceCents);

public class InventoryClient(HttpClient httpClient)
{
    /// <summary>Returns null when the SKU is unknown to the inventory service.</summary>
    public async Task<StockItem?> GetStockAsync(string sku, CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync($"/stock/{Uri.EscapeDataString(sku)}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<StockItem>(ct)
               ?? throw new InvalidOperationException($"Inventory returned an empty body for SKU '{sku}'.");
    }
}

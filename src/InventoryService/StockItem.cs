namespace InventoryService;

public class StockItem
{
    public required string Sku { get; init; }

    public required int AvailableQuantity { get; set; }

    public required int UnitPriceCents { get; set; }
}

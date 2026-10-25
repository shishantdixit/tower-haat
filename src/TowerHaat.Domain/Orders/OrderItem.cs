using TowerHaat.Domain.Catalog;
using TowerHaat.Domain.Common;

namespace TowerHaat.Domain.Orders;

/// <summary>A line of an order with the item's name and price as they were when ordered.</summary>
public class OrderItem : Entity, ITenantEntity
{
    public const int MaxQuantity = 50;

    private OrderItem()
    {
    }

    internal OrderItem(Order order, CatalogItem item, int quantity)
    {
        if (quantity is < 1 or > MaxQuantity)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), $"Quantity must be between 1 and {MaxQuantity}.");
        }

        SocietyId = order.SocietyId;
        OrderId = order.Id;
        CatalogItemId = item.Id;
        NameSnapshot = item.Name;
        PriceSnapshot = item.Price;
        IsVegSnapshot = item.IsVeg;
        Quantity = quantity;
        LineTotal = item.Price * quantity;
    }

    public Guid SocietyId { get; set; }

    public Guid OrderId { get; private set; }

    /// <summary>The catalog item ordered; it may since have changed or been deleted.</summary>
    public Guid CatalogItemId { get; private set; }

    public string NameSnapshot { get; private set; } = null!;

    public decimal PriceSnapshot { get; private set; }

    public bool? IsVegSnapshot { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal { get; private set; }
}

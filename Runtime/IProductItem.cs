using JetBrains.Annotations;

namespace GameKit.Purchasing
{
    public interface IProductItem
    {
        string Id { get; }
        string StoreId { get; }
        ProductItemType Type { get; }
        IProductPrice Price { get; set; }
        ProductStatus Status { get; set; }
    }

    public enum ProductStatus
    {
        None,
        Ready,
        Pending,
        Purchased
    }
    
    public enum ProductItemType
    {
        Consumable,
        NonConsumable,
        Subscription
    }

    public interface IProductPrice
    {
        decimal Localized { get; }
        string CurrencyIsoCode { get; }
    }

    [PublicAPI]
    public class ProductItem: IProductItem
    {
        public string Id { get; }
        public string StoreId { get; }
        public ProductItemType Type { get; }
        public IProductPrice Price { get; set; }
        public ProductStatus Status { get; set; }

        public ProductItem(string id, string storeId, ProductItemType type)
        {
            Id = id;
            StoreId = storeId;
            Type = type;
        }
    }
}
using JetBrains.Annotations;

namespace GameKit.Purchasing
{
    [PublicAPI]
    public class ProductItem: IProductItem
    {
        public string Id { get; }
        public string StoreId { get; }
        public ProductItemType Type { get; }
        public IProductPrice Price { get; set; }
        public ProductStatus Status { get; set; }

        public ProductItem( ProductItemType type, string id, string storeId = null)
        {
            Id = id;
            Type = type;
            StoreId = string.IsNullOrWhiteSpace(storeId) ? id : storeId;
        }
    }
}
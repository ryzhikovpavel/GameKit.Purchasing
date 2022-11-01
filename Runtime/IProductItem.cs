namespace GameKit.Purchasing
{
    public interface IProductItem
    {
        string Id { get; }
        string StoreId { get; }
        ProductItemType Type { get; }
        Price Price { get; }
        ProductStatus Status { get; set; }
    }
}
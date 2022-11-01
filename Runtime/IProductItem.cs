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
}
namespace GameKit.Purchasing
{
    public interface IProduct
    {
        string Id { get; }
        string StoreId { get; }
        ProductType Type { get; }
        Price Price { get; set; }
        bool IsPending { get; set; }
    }

    public enum ProductType
    {
        Consumable,
        NonConsumable,
        Subscription
    }
    
    public struct Price
    {
        public decimal Value;
        public string Currency;
    }
}
namespace GameKit.Purchasing
{
    public interface IProductPrice
    {
        decimal Value { get; }
        string CurrencyIsoCode { get; }
    }
}
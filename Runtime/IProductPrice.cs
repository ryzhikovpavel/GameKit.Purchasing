namespace GameKit.Purchasing
{
    public interface IProductPrice
    {
        decimal Localized { get; }
        string CurrencyIsoCode { get; }
    }
}
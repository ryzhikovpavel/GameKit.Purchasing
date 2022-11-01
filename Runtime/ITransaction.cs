namespace GameKit.Purchasing
{
    public interface ITransaction
    {
        string Id { get; }
        TransactionState State { get; }
        string Error { get; }
    }
    
    public interface ITransaction<out TProduct>: ITransaction where TProduct: IProductItem
    {
        TProduct Product { get; }
    }
}
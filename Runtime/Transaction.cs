namespace GameKit.Purchasing
{
    public class Transaction<TProduct>: ITransaction<TProduct> where TProduct : IProduct
    {
        public Transaction(TProduct product, string id)
        {
            Product = product;
            Id = id;
        }

        public TProduct Product { get; }
        
        public string Id { get; }
        public TransactionState State { get; set; }
        public string Error { get; set; }
    }
}
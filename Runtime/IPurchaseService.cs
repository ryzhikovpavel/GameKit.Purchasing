using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GameKit.Purchasing
{
    public interface IPurchaseService<TProduct>: IEnumerable<TProduct> where TProduct : class, IProductItem
    {
        event Action<ITransaction<TProduct>> EventTransactionBegin;
        event Action<ITransaction<TProduct>> EventTransactionSuccess;
        event Action<ITransaction<TProduct>> EventTransactionPending;
        event Action<ITransaction<TProduct>> EventTransactionCanceled;
        event Action<ITransaction<TProduct>> EventTransactionFailed;
        event Action<ITransaction<TProduct>> EventTransactionCompleted;
        event Action<TProduct> EventProductPurchased; 

        bool IsInitialized { get; }

        bool FindProduct(string productId, out TProduct product);
        Task Initialize(params TProduct[] products);
        void Confirm(TProduct product);
        Task Restore();
        Task<ITransaction<TProduct>> Purchase(Transaction<TProduct> transaction);
        public Task<ITransaction<TProduct>> Purchase(string productId)
        {
            if (FindProduct(productId, out TProduct product) == false) throw new Exception("Product not found");
            return Purchase(product);
        }
        
        public Task<ITransaction<TProduct>> Purchase(TProduct product)
        {
            return Purchase(new Transaction<TProduct>(product, Guid.NewGuid().ToString()));
        }
        
        public void Purchase(string productId, Action<ITransaction<TProduct>> completed)
        {
            if (FindProduct(productId, out TProduct product) == false) throw new Exception("Product not found");
            Purchase(product, completed);
        }

        public void Purchase(TProduct product, Action<ITransaction<TProduct>> completed)
        {
            Purchase(product).ContinueWith((t) =>
            {
                completed?.Invoke(t.Result);
            });
        }
    }
}
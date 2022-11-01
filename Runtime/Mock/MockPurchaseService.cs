using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GameKit.Purchasing.Mock
{
    public class MockPurchaseService<TProduct>: IPurchaseService<TProduct> where TProduct : class, IProductItem
    {
        public event Action<ITransaction<TProduct>> EventTransactionBegin;

        public event Action<ITransaction<TProduct>> EventTransactionSuccess;

        public event Action<ITransaction<TProduct>> EventTransactionPending;

        public event Action<ITransaction<TProduct>> EventTransactionCanceled;

        public event Action<ITransaction<TProduct>> EventTransactionFailed;

        public event Action<ITransaction<TProduct>> EventTransactionCompleted;

        public event Action<TProduct> EventProductPurchased;

        public bool IsInitialized { get; }

        public bool FindProduct(string productId, out TProduct product)
        {
            throw new NotImplementedException();
        }

        public Task Initialize(params TProduct[] products)
        {
            throw new NotImplementedException();
        }

        public void Confirm(TProduct product)
        {
            throw new NotImplementedException();
        }

        public void Restore()
        {
            throw new NotImplementedException();
        }

        public Task<ITransaction<TProduct>> Purchase(Transaction<TProduct> transaction)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<TProduct> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace GameKit.Purchasing.Mock
{
    public class MockPurchaseService<TProduct>: IPurchaseService<TProduct> where TProduct : class, IProductItem
    {
        private TProduct[] _products;
        public event Action<ITransaction<TProduct>> EventTransactionBegin;

        public event Action<ITransaction<TProduct>> EventTransactionSuccess;

        public event Action<ITransaction<TProduct>> EventTransactionPending;

        public event Action<ITransaction<TProduct>> EventTransactionCanceled;

        public event Action<ITransaction<TProduct>> EventTransactionFailed;

        public event Action<ITransaction<TProduct>> EventTransactionCompleted;

        public event Action<TProduct> EventProductPurchased;

        public bool IsInitialized { get; private set; }

        public async Task Initialize(params TProduct[] products)
        {
            _products = products;
            await Task.Delay(1000);
            IsInitialized = true;
            Debug.Log($"IAP initialized");
        }

        public bool FindProduct(string productId, out TProduct product)
        {
            foreach (TProduct item in _products)
            {
                if (item.Id == productId)
                {
                    product = item;
                    return true;
                }
            }

            product = default;
            return false;
        }

        public void Confirm(TProduct product)
        {
            Debug.Log($"IAP Purchase: {product.Id}");
            if (product.Type == ProductItemType.Consumable)
                product.Status = ProductStatus.Ready;
            else
                product.Status = ProductStatus.Purchased;
        }

        public async Task Restore()
        {
            Debug.Log("IAP Restore being");
            await Task.Delay(1000);
            Debug.Log("IAP Restore completed");
        }

        public async Task<ITransaction<TProduct>> Purchase(Transaction<TProduct> transaction)
        {
            Debug.Log($"IAP Purchase: {transaction.Product.Id}");
            transaction.State = TransactionState.Processing;
            await Task.Delay(1000);
            transaction.State = TransactionState.Pending;
            transaction.Product.Status = ProductStatus.Pending;
            return transaction;
        }

        public IEnumerator<TProduct> GetEnumerator()
            => ((IEnumerable<TProduct>)_products).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
}
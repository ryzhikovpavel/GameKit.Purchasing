using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace GameKit.Purchasing.Mock
{
    public class MockPurchaseService<TProduct>: IPurchaseService<TProduct> where TProduct : class, IProductItem
    {
        private readonly List<Transaction<TProduct>> _transactions = new List<Transaction<TProduct>>();
        private TProduct[] _products;
        public event Action EventInitialized;
        public event Action<ITransaction<TProduct>> EventTransactionBegin;
        public event Action<ITransaction<TProduct>> EventTransactionCompleted;
        public event Action<TProduct> EventProductPurchased;

        public bool IsInitialized { get; private set; }

        public async Task Initialize(params TProduct[] products)
        {
            _products = products;
            await Task.Delay(1000);
            IsInitialized = true;
            Debug.Log($"IAP initialized");
            EventInitialized?.Invoke();
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
            Debug.Log($"IAP Confirm: {product.Id}");
            if (product.Type == ProductItemType.Consumable)
                product.Status = ProductStatus.Ready;
            else
                product.Status = ProductStatus.Purchased;

            if (FindTransaction(product.StoreId, out var transaction))
            {
                transaction.State = TransactionState.Successful;
                _transactions.Remove(transaction);
            }
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
            _transactions.Add(transaction);
            transaction.State = TransactionState.Processing;
            transaction.Product.Status = ProductStatus.Pending;
            EventTransactionBegin?.Invoke(transaction);
            await Task.Delay(1000);

            transaction.State = TransactionState.Successful;
            switch (transaction.Product.Type)
            {
                case ProductItemType.Consumable:
                    transaction.Product.Status = ProductStatus.Ready;
                    break;
                case ProductItemType.NonConsumable:
                    transaction.Product.Status = ProductStatus.Purchased;
                    break;
                case ProductItemType.Subscription:
                    transaction.Product.Status = ProductStatus.Purchased;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            EventProductPurchased?.Invoke(transaction.Product);
            EventTransactionCompleted?.Invoke(transaction);

            return transaction;
        }

        public IEnumerator<TProduct> GetEnumerator()
            => ((IEnumerable<TProduct>)_products).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
        
        private bool FindTransaction(string storeId, out Transaction<TProduct> transaction)
        {
            foreach (var t in _transactions)
            {
                if (t.Product.StoreId.Equals(storeId))
                {
                    transaction = t;
                    return true;
                }
            }

            transaction = default;
            return false;
        }
    }
}
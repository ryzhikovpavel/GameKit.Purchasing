#if UnityPurchasingApi
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Purchasing;

namespace GameKit.Purchasing
{
    [PublicAPI]
    public class UnityPurchasingService<TProduct>: IPurchaseService<TProduct> where TProduct : class, IProductItem
    {
        // ReSharper disable once InconsistentNaming
        private readonly ILogger Debug;
        private readonly ITransactionValidator _validator;
        private UnityPurchasingStoreListener _store;
        private TProduct[] _products;
        
        private List<Transaction<TProduct>> _transactions = new List<Transaction<TProduct>>();

        public event Action EventInitialized;
        public event Action<ITransaction<TProduct>> EventTransactionBegin;
        public event Action<ITransaction<TProduct>> EventTransactionCompleted;
        public event Action<TProduct> EventProductPurchased;

        public bool IsInitialized { get; private set; }

        public UnityPurchasingService() : this(new MockValidator(), UnityEngine.Debug.unityLogger) { }
        public UnityPurchasingService(ILogger logger) : this(new MockValidator(), logger) { }
        public UnityPurchasingService(ITransactionValidator validator) : this(validator, UnityEngine.Debug.unityLogger) { }
        public UnityPurchasingService(ITransactionValidator validator, ILogger logger)
        {
            _validator = validator;
            Debug = logger;
        }

        public async Task Initialize(params TProduct[] products)
        {
            _products = products;

            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                await UnityServices.InitializeAsync();

            ConfigurationBuilder builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            foreach (var product in products)
                builder.AddProduct(product.StoreId, product.GetUnityProductType());
            
            IsInitialized = false;
            
            _store = new UnityPurchasingStoreListener(Debug, builder);
            _store.EventPurchased += CompletePurchase;
            _store.EventPurchaseFailed += OnPurchaseFailed;
            _store.EventPurchaseDeferred += OnPurchaseDeferred;

            while (_store.IsInitializing)
            {
                await Task.Yield();
                if (Application.isPlaying == false) throw new Exception("Application is shutdown");
            }

            if (_store.IsInitialized == false)
                throw new Exception("Initialize IAP Service failed");

            SyncItems();
            if (Debug.IsLogTypeAllowed(LogType.Log)) Debug.Log("Initialized");
            
            IsInitialized = true;
            EventInitialized?.Invoke();
        }

        public void Confirm(TProduct product)
        {
            Product p = _store.Products.WithID(product.StoreId);
            _store.ConfirmPendingPurchase(p);
        }

        public async Task Restore()
        {
            bool wait = true;

            void OnCompleted(bool b)
            {
                wait = false;
            }
            
            _store.Restore(OnCompleted);

            while (wait)
            {
                await Task.Yield();
                if (Application.isPlaying == false)
                    throw new Exception("Application is shutdown");
            }
        }

        public bool FindProduct(string productId, out TProduct product)
        {
            foreach (TProduct p in _products)
            {
                if (p.Id == productId)
                {
                    product = p;
                    return true;
                }
            }

            product = default;
            return false;
        }

        public async Task<ITransaction<TProduct>> Purchase(Transaction<TProduct> transaction)
        {
            if (Debug.IsLogTypeAllowed(LogType.Log)) Debug.Log($"Purchase '{transaction.Product.Id}' product");
            _transactions.Add(transaction);
            transaction.State = TransactionState.Processing;
            try
            {
                EventTransactionBegin?.Invoke(transaction);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            if (Debug.IsLogTypeAllowed(LogType.Log))
            {
                var p = _store.Products.WithID(transaction.Product.StoreId);
                if (p.hasReceipt)
                    Debug.Log($"'{transaction.Product.Id}' product has receipt: {p.receipt}");
            }
            _store.InitiatePurchase(transaction.Product.StoreId);

            while (transaction.State == TransactionState.Processing)
            {
                await Task.Yield();
                if (Application.isPlaying == false) throw new Exception("Application is shutdown");
            }

            try
            {
                EventTransactionCompleted?.Invoke(transaction);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            return transaction;
        }

        public IEnumerator<TProduct> GetEnumerator() 
            => ((IEnumerable<TProduct>)_products).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        private void SyncItems()
        {
            foreach (TProduct item in _products)
            {
                Product product = _store.Products.WithID(item.StoreId);
                if (product == null)
                {
                    Debug.Log(LogType.Error, $"Product '{item.StoreId}' not found");
                    continue;
                }

                if (Debug.IsLogTypeAllowed(LogType.Log))
                {
                    Debug.Log($"Product: {product.definition.id}|Receipt:{product.receipt}");
                }

                if (Application.isEditor == false)
                {
                    item.Price.StoreValue = product.metadata.localizedPrice;
                    item.Price.StoreCurrencyIsoCode = product.metadata.isoCurrencyCode;
                    item.Price.StoreValueWithCurrency = product.metadata.localizedPriceString;
                }

                if (product.availableToPurchase == false)
                {
                    item.Status = ProductStatus.None;
                    continue;
                }

                if (product.hasReceipt)
                {
                    if (_store.IsPurchasedProductDeferred(product) == false)
                        item.Status = ProductStatus.Pending;
                    else
                        item.Status = ProductStatus.Purchased;
                }
                else
                {
                    item.Status = ProductStatus.Ready;
                }
            }
        }

        private void OnPurchaseDeferred(Product product)
        {
            if (FindTransaction(product.definition.id, out var transaction))
            {
                transaction.State = TransactionState.Deferred;
            }

            if (FindProductByStoreId(product.definition.id, out var item))
                item.Status = ProductStatus.Pending;
        }

        private void OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            if (failureReason == PurchaseFailureReason.DuplicateTransaction)
            {
                CompletePurchase(product);
                return;
            }
            
            if (FindTransaction(product.definition.id, out var transaction))
            {
                transaction.Error = failureReason.ToString();
                transaction.State = failureReason == PurchaseFailureReason.UserCancelled ? TransactionState.Canceled : TransactionState.Failed;
            }

            if (FindProductByStoreId(product.definition.id, out var item))
                item.Status = ProductStatus.Ready;
        }

        private void CompletePurchase(Product product)
        {
            if (Debug.IsLogTypeAllowed(LogType.Log))
                Debug.Log(LogType.Log, $"'{product.definition.id}' product purchased with receipt: {product.receipt}");
            
            if (FindTransaction(product.definition.id, out var transaction))
            {
                if (transaction.State == TransactionState.Successful)
                    Debug.Log(LogType.Error, $"{transaction.Product.Id} Transaction is already completed");
                transaction.State = TransactionState.Successful;
                _transactions.Remove(transaction);
            }

            if (FindProductByStoreId(product.definition.id, out var p))
            {
                EventProductPurchased?.Invoke(p);
            }
            else
            if (Debug.IsLogTypeAllowed(LogType.Error))
                Debug.Log(LogType.Error, $"Not found product with '{product.definition.id}' in ProcessPurchase");
        }

        private bool FindProductByStoreId(string storeId, out TProduct product)
        {
            foreach (TProduct p in _products)
            {
                if (p.StoreId == storeId)
                {
                    product = p;
                    return true;
                }
            }

            product = default;
            return false;
        }

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
#endif
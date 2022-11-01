#if UnityPurchasingApi
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Purchasing;

namespace GameKit.Purchasing
{
    public class UnityPurchasingService<TProduct>: IStoreListener, IPurchaseService<TProduct> where TProduct : IProduct
    {
        // ReSharper disable once InconsistentNaming
        private readonly ILogger Debug;
        private readonly ITransactionValidator _validator;
        private IStoreController _storeController;
        private IExtensionProvider _extensions;
        private TProduct[] _products;
        
        private string _error;
        private bool _processing;
        private List<Transaction<TProduct>> _transactions;

        public event Action<ITransaction<TProduct>> EventTransactionBegin;
        public event Action<ITransaction<TProduct>> EventTransactionSuccess;
        public event Action<ITransaction<TProduct>> EventTransactionPending;
        public event Action<ITransaction<TProduct>> EventTransactionCanceled;
        public event Action<ITransaction<TProduct>> EventTransactionFailed;
        public event Action<ITransaction<TProduct>> EventTransactionCompleted;
        public event Action<TProduct> EventProductPurchased;

        public bool IsInitialized { get; private set; }

        public UnityPurchasingService() : this(new MockValidator(), UnityEngine.Debug.unityLogger) { }
        public UnityPurchasingService(ILogger logger) : this(new MockValidator(), logger) { }
        public UnityPurchasingService(ITransactionValidator validator, ILogger logger)
        {
            _validator = validator;
            Debug = logger;
        }

        public async Task Initialize(TProduct[] products)
        {
            _products = products;

            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                await UnityServices.InitializeAsync(new InitializationOptions());
            
            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance());
            foreach (var product in products)
                builder.AddProduct(product.StoreId, product.GetUnityProductType());

            _processing = true;
            IsInitialized = false;
            
            builder.Configure<IGooglePlayConfiguration>().SetDeferredPurchaseListener(OnDeferredPurchase);
            
            UnityPurchasing.Initialize(this, builder);

            while (_processing)
            {
                await Task.Yield();
                if (Application.isPlaying == false) throw new Exception("Application is shutdown");
            }

            if (IsInitialized == false)
                throw new Exception("Initialize IAP Service failed: " + _error);
        }

        public void ConfirmAccrual(TProduct product)
        {
            var p = _storeController.products.WithID(product.StoreId);
            _storeController.ConfirmPendingPurchase(p);
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
            if (Debug.IsLogTypeAllowed(LogType.Log)) Debug.Log($"Purchase {transaction.Product.Id} product");
            _processing = true;
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
            
            _storeController.InitiatePurchase(transaction.Product.StoreId);

            while (transaction.State == TransactionState.Processing)
            {
                await Task.Yield();
                if (Application.isPlaying == false) throw new Exception("Application is shutdown");
            }

            try
            {
                switch (transaction.State)
                {
                    case TransactionState.Created:
                    case TransactionState.Processing:
                        Debug.Log(LogType.Error, "Transaction state broken: " + transaction.State.ToString());
                        break;
                    case TransactionState.Pending:
                        EventTransactionPending?.Invoke(transaction);
                        break;
                    case TransactionState.Failed:
                        EventTransactionFailed?.Invoke(transaction);
                        break;
                    case TransactionState.Canceled:
                        EventTransactionCanceled?.Invoke(transaction);
                        break;
                    case TransactionState.Successful:
                        EventTransactionSuccess?.Invoke(transaction);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
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

        void IStoreListener.OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            if (Debug.IsLogTypeAllowed(LogType.Log)) Debug.Log("Initialized");
            _storeController = controller;
            _extensions = extensions;
            IsInitialized = true;
            _processing = false;

            var apple = _extensions.GetExtension<IAppleExtensions>();
            if (apple != null)
                apple.RegisterPurchaseDeferredListener( OnDeferredPurchase );
        }

        void IStoreListener.OnInitializeFailed(InitializationFailureReason error)
        {
            if (Debug.IsLogTypeAllowed(LogType.Error)) Debug.Log($"Initialize failed: {error}");
            _error = error.ToString();
            _processing = false;
        }

        PurchaseProcessingResult IStoreListener.ProcessPurchase(PurchaseEventArgs purchaseEvent)
        {
            var product = purchaseEvent.purchasedProduct;
            if (Debug.IsLogTypeAllowed(LogType.Log)) Debug.Log($"ProcessPurchase - Product: '{product.definition.id}'");

            if (IsPurchasedProductDeferred(product)) return PurchaseProcessingResult.Pending;
            if (product.hasReceipt == false) return PurchaseProcessingResult.Pending;

            void OnValidated(bool result)
            {
                if (result) CompletePurchase(product);
            }
            _validator.Validate(product.receipt, OnValidated);
            return PurchaseProcessingResult.Pending;
        }

        void IStoreListener.OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            if (Debug.IsLogTypeAllowed(LogType.Error)) Debug.Log(LogType.Error, $"Purchase failed - Product: '{product.definition.id}', PurchaseFailureReason: {failureReason}");
            _error = failureReason.ToString();
            if (FindTransaction(product.definition.id, out var transaction))
            {
                transaction.Error = _error;
                transaction.State = failureReason == PurchaseFailureReason.UserCancelled ? TransactionState.Canceled : TransactionState.Failed;
            }
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

        private void CompletePurchase(Product product)
        {
            if (FindTransaction(product.definition.id, out var transaction))
            {
                if (transaction.State == TransactionState.Successful)
                    Debug.Log(LogType.Error, $"{transaction.Product.Id} Transaction is already completed");
                transaction.State = TransactionState.Successful;
            }

            if (FindProductByStoreId(product.definition.id, out var p))
                EventProductPurchased?.Invoke(p);
            else
            if (Debug.IsLogTypeAllowed(LogType.Error))
                Debug.Log(LogType.Error, $"Not found product with '{product.definition.id}' in ProcessPurchase");
        }

        private void OnDeferredPurchase(Product product)
        {
            Debug.Log($"OnDeferredPurchase {product.definition.id}");
            CompletePurchase(product);
        }

        private bool IsPurchasedProductDeferred(Product product)
        {
            var google = _extensions.GetExtension<IGooglePlayStoreExtensions>();
            if (google != null)  
                return google.IsPurchasedProductDeferred(product);
            
            return false;
        }
    }

    internal static class ProductExtension
    {
        public static UnityEngine.Purchasing.ProductType GetUnityProductType(this IProduct product)
        {
            switch (product.Type)
            {
                case ProductType.Consumable: return UnityEngine.Purchasing.ProductType.Consumable;
                case ProductType.NonConsumable:  return UnityEngine.Purchasing.ProductType.NonConsumable;
                case ProductType.Subscription: return UnityEngine.Purchasing.ProductType.Subscription;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
#endif
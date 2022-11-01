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
    public class UnityPurchasingService<TProduct>: IStoreListener, IPurchaseService<TProduct> where TProduct : class, IProductItem
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

        public async Task Initialize(params TProduct[] products)
        {
            _products = products;

            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                await UnityServices.InitializeAsync();

            var module = StandardPurchasingModule.Instance();
            module.useFakeStoreUIMode = FakeStoreUIMode.DeveloperUser;
            var builder = ConfigurationBuilder.Instance(module);
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

        public void Confirm(TProduct product)
        {
            var p = _storeController.products.WithID(product.StoreId);
            _storeController.ConfirmPendingPurchase(p);
        }

        public async Task Restore()
        {
            bool wait = true;

            void OnCompleted(bool b)
            {
                wait = false;
            }
            
            if (Application.platform == RuntimePlatform.WSAPlayerX86 ||
                Application.platform == RuntimePlatform.WSAPlayerX64 ||
                Application.platform == RuntimePlatform.WSAPlayerARM)
            {
                _extensions.GetExtension<IMicrosoftExtensions>().RestoreTransactions();
                return;
            }
            else if (Application.platform == RuntimePlatform.IPhonePlayer ||
                     Application.platform == RuntimePlatform.OSXPlayer ||
                     Application.platform == RuntimePlatform.tvOS)
            {
                _extensions.GetExtension<IAppleExtensions>().RestoreTransactions(OnCompleted);
                
            }
            else if (Application.platform == RuntimePlatform.Android &&
                     StandardPurchasingModule.Instance().appStore == AppStore.GooglePlay)
            {
                _extensions.GetExtension<IGooglePlayStoreExtensions>().RestoreTransactions(OnCompleted);
            }
            else
            {
                Debug.Log(LogType.Warning,Application.platform.ToString() +
                                             " is not a supported platform for the Codeless IAP restore button");
            }

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

            foreach (TProduct item in _products)
            {
                Product product = _storeController.products.WithID(item.StoreId);
                if (product == null)
                {
                    Debug.Log(LogType.Error, $"Product '{item.StoreId}' not found");
                    continue;
                }

                if (Debug.IsLogTypeAllowed(LogType.Log))
                {
                    Debug.Log($"Product: {product.definition.id}");
                }
                
                item.Price.StoreValue = product.metadata.localizedPrice;
                item.Price.StoreCurrencyIsoCode = product.metadata.isoCurrencyCode;
                item.Price.StoreValueWithCurrency = product.metadata.localizedPriceString;

                if (product.availableToPurchase == false)
                {
                    item.Status = ProductStatus.None;
                    continue;
                }

                if (product.hasReceipt)
                {
                    item.Status = ProductStatus.Purchased;
                }
                else
                {
                    item.Status = ProductStatus.Ready;
                }
            }
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

            if (FindProductByStoreId(product.definition.id, out var item))
                item.Status = ProductStatus.Pending;

            if (IsPurchasedProductDeferred(product))
            {
                if (FindTransaction(product.definition.id, out var transaction))
                    transaction.State = TransactionState.Pending;
                return PurchaseProcessingResult.Pending;
            }

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

            if (FindProductByStoreId(product.definition.id, out var item))
                item.Status = ProductStatus.Ready;
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
        public static ProductType GetUnityProductType(this IProductItem product)
        {
            switch (product.Type)
            {
                case ProductItemType.Consumable: return ProductType.Consumable;
                case ProductItemType.NonConsumable:  return ProductType.NonConsumable;
                case ProductItemType.Subscription: return ProductType.Subscription;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
#endif
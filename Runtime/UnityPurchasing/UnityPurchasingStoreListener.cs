#if UnityPurchasingApi
using System;
using UnityEngine;
using UnityEngine.Purchasing;

namespace GameKit.Purchasing
{
    public class UnityPurchasingStoreListener: IStoreListener
    {
        // ReSharper disable once InconsistentNaming
        private readonly ILogger Debug;
        private IStoreController _storeController;
        private IExtensionProvider _extensions;

        public event Action<Product> EventPurchased;
        public event Action<Product> EventPurchaseDeferred;
        public event Action<Product, PurchaseFailureReason> EventPurchaseFailed;

        public bool IsInitializing { get; private set; }
        public bool IsInitialized { get; private set; }
        public ProductCollection Products => _storeController.products;

        public UnityPurchasingStoreListener(ILogger logger, ConfigurationBuilder builder)
        {
            Debug = logger;
            IsInitialized = false;
            IsInitializing = true;
            
            builder.Configure<IGooglePlayConfiguration>().SetDeferredPurchaseListener(OnDeferredPurchase);
            UnityPurchasing.Initialize(this, builder);
        }
        
        public void ConfirmPendingPurchase(Product product)
            => _storeController.ConfirmPendingPurchase(product);

        public void Restore(Action<bool> completed)
        {
            if (Application.platform == RuntimePlatform.WSAPlayerX86 ||
                Application.platform == RuntimePlatform.WSAPlayerX64 ||
                Application.platform == RuntimePlatform.WSAPlayerARM)
            {
                _extensions.GetExtension<IMicrosoftExtensions>().RestoreTransactions();
                completed?.Invoke(true);
            }
            else if (Application.platform == RuntimePlatform.IPhonePlayer ||
                     Application.platform == RuntimePlatform.OSXPlayer ||
                     Application.platform == RuntimePlatform.tvOS)
            {
                _extensions.GetExtension<IAppleExtensions>().RestoreTransactions(completed);
                
            }
            else if (Application.platform == RuntimePlatform.Android &&
                     StandardPurchasingModule.Instance().appStore == AppStore.GooglePlay)
            {
                _extensions.GetExtension<IGooglePlayStoreExtensions>().RestoreTransactions(completed);
            }
            else
            {
                Debug.Log(LogType.Warning,Application.platform.ToString() +
                                          " is not a supported platform for the Codeless IAP restore button");
                completed?.Invoke(false);
            }
        }

        public void InitiatePurchase(string productStoreId)
            => _storeController.InitiatePurchase(productStoreId);

        void IStoreListener.OnInitialized(IStoreController controller, IExtensionProvider extensions)
        {
            if (Debug.IsLogTypeAllowed(LogType.Log)) Debug.Log("Initialized");
            _storeController = controller;
            _extensions = extensions;
            IsInitialized = true;
            IsInitializing = false;

            var apple = _extensions.GetExtension<IAppleExtensions>();
            if (apple != null)
                apple.RegisterPurchaseDeferredListener( OnDeferredPurchase );
        }

        void IStoreListener.OnInitializeFailed(InitializationFailureReason error)
        {
            if (Debug.IsLogTypeAllowed(LogType.Error)) Debug.Log($"Initialize failed: {error}");
            IsInitialized = false;
            IsInitializing = false;
        }

        PurchaseProcessingResult IStoreListener.ProcessPurchase(PurchaseEventArgs purchaseEvent)
        {
            var product = purchaseEvent.purchasedProduct;
            if (Debug.IsLogTypeAllowed(LogType.Log)) Debug.Log($"ProcessPurchase - Product: '{product.definition.id}', Receipt: {product.receipt}");

            if (IsPurchasedProductDeferred(product))
            {
                Debug.Log(LogType.Error, $"Purchase deferred - Product: '{product.definition.id}', Receipt: {product.receipt}");
                EventPurchaseDeferred?.Invoke(product);
                return PurchaseProcessingResult.Pending;
            }

            EventPurchased?.Invoke(product);
            return PurchaseProcessingResult.Pending;
        }

        void IStoreListener.OnPurchaseFailed(Product product, PurchaseFailureReason failureReason)
        {
            if (Debug.IsLogTypeAllowed(LogType.Error)) 
                Debug.Log(LogType.Error, $"Purchase failed - Product: '{product.definition.id}', PurchaseFailureReason: {failureReason}, Receipt: {product.receipt}");
            EventPurchaseFailed?.Invoke(product, failureReason);
        }
        
        private void OnDeferredPurchase(Product product)
        {
            if (Debug.IsLogTypeAllowed(LogType.Log)) 
                Debug.Log($"OnDeferredPurchase {product.definition.id}, Receipt: {product.receipt}");
            EventPurchaseDeferred?.Invoke(product);
        }

        public bool IsPurchasedProductDeferred(Product product)
        {
            var google = _extensions.GetExtension<IGooglePlayStoreExtensions>();
            if (google != null)  
                return google.IsPurchasedProductDeferred(product);
            
            return true;
        }
    }
    #endif
}

using System;
using JetBrains.Annotations;
using UnityEngine;

namespace GameKit.Purchasing
{
    [PublicAPI]
    [Serializable]
    public class ProductItem: IProductItem
    {
        [SerializeField] private string id;
        [SerializeField] private ProductItemType type;
        [SerializeField] private Price price;
        
        [Header("Store Ids")]
        [SerializeField] private string googleId;
        [SerializeField] private string appleId;

        public string Id => id;
        public string StoreId => GetStoreId();
        public ProductItemType Type => type;
        public Price Price => price;
        public ProductStatus Status { get; set; }

        public ProductItem(){}
        public ProductItem( ProductItemType type, string id, uint priceInCents, string googleId = null, string appleId = null)
        {
            id = id;
            type = type;
            this.googleId = string.IsNullOrWhiteSpace(googleId) ? id : googleId;
            this.appleId = string.IsNullOrWhiteSpace(appleId) ? id : appleId;
            this.price = new Price(priceInCents);
        }

        private string GetStoreId()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                    return googleId;
                case RuntimePlatform.IPhonePlayer:
                    return appleId;
            }

            return id;
        }
    }
}
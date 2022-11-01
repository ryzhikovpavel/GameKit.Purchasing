#if UnityPurchasingApi
using System;
using UnityEngine.Purchasing;

namespace GameKit.Purchasing
{
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
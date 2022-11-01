using System;
using UnityEngine;

namespace GameKit.Purchasing
{
    [Serializable]
    public class Price
    {
        [SerializeField] private uint inCents;

        public decimal StoreValue { get; set; }
        public string StoreCurrencyIsoCode { get; set; }
        internal string StoreValueWithCurrency { get; set; }
        public uint Cents => inCents;

        public Price() { }
        public Price(uint priceInCents)
        {
            inCents = priceInCents;
        }

        public override string ToString()
        {
            if (string.IsNullOrWhiteSpace(StoreValueWithCurrency))
            {
                if (StoreValue == 0) return $"${Cents/100:C}";
                else return $"{StoreValue} {StoreCurrencyIsoCode}";
            }

            return StoreValueWithCurrency;
        }
    }
}
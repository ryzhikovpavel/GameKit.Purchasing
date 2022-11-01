using System;
using System.Threading.Tasks;

namespace GameKit.Purchasing
{
    public interface ITransactionValidator
    {
        void Validate(string receipt, Action<bool> completed);
    }

    public class MockValidator : ITransactionValidator
    {
        public void Validate(string receipt, Action<bool> completed)
        {
            completed?.Invoke(true);
        }
    }
}
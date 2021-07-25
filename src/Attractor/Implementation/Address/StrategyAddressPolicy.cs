using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation.Address
{
    internal sealed class StrategyAddressPolicy : IAddressPolicy
    {
        private readonly Func<IAddress, CancellationToken, ValueTask<bool>> strategy;

        public StrategyAddressPolicy(Func<IAddress, CancellationToken, ValueTask<bool>> strategy)
        {
            this.strategy = strategy;
        }

        public ValueTask<bool> IsMatchAsync(IAddress address, CancellationToken token = default)
        {
            return strategy(address, token);
        }
    }
}

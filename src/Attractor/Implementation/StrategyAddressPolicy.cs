using System;

namespace Attractor.Implementation
{
    internal sealed class StrategyAddressPolicy : IAddressPolicy
    {
        private readonly Func<IAddress, bool> strategy;

        public StrategyAddressPolicy(Func<IAddress, bool> strategy)
        {
            this.strategy = strategy;
        }

        public bool IsMatch(IAddress address)
        {
            return strategy(address);
        }
    }
}

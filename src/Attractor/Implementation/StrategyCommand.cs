using System;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class StrategyCommand : ICommand
    {
        private readonly Action syncStrategy;
        private readonly Func<ValueTask> asyncStrategy;

        public StrategyCommand(Action syncStrategy)
        {
            this.syncStrategy = syncStrategy ?? throw new ArgumentNullException(nameof(syncStrategy));
        }

        public StrategyCommand(Func<ValueTask> asyncStrategy)
        {
            this.asyncStrategy = asyncStrategy ?? throw new ArgumentNullException(nameof(asyncStrategy));
        }

        ValueTask ICommand.ExecuteAsync()
        {
            if (syncStrategy == null)
            {
                return asyncStrategy();
            }

            syncStrategy();

            return default;
        }
    }
}
using System;
using System.Threading.Tasks;

namespace TractorNet.Implementation.Common
{
    internal sealed class StrategyDisposable : IAsyncDisposable
    {
        private readonly Func<ValueTask> asyncAction;

        public StrategyDisposable(Func<ValueTask> asyncAction)
        {
            this.asyncAction = asyncAction;
        }

        public StrategyDisposable(Action action)
        {
            asyncAction = () =>
            {
                action();

                return ValueTask.CompletedTask;
            };
        }

        public ValueTask DisposeAsync()
        {
            return asyncAction();
        }
    }
}

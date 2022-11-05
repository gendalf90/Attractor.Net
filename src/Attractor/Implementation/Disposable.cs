using System;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal static class Disposable
    {
        public static IUsable Empty { get; } = new EmptyUsable();

        public static IUsable Create(Action strategy)
        {
            return new StrategyUsable(strategy);
        }

        public static IAsyncDisposable Create(Func<ValueTask> strategy)
        {
            return new StrategyUsable(strategy);
        }

        private class EmptyUsable : IUsable
        {
            public void Dispose()
            {
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }

        private class StrategyUsable : IUsable
        {
            private readonly Func<ValueTask> asyncStrategy;
            private readonly Action syncStrategy;

            public StrategyUsable(Action syncStrategy)
            {
                this.syncStrategy = syncStrategy;
            }

            public StrategyUsable(Func<ValueTask> asyncStrategy)
            {
                this.asyncStrategy = asyncStrategy;
            }

            public void Dispose()
            {
                syncStrategy?.Invoke();
            }

            public ValueTask DisposeAsync()
            {
                if (asyncStrategy != null)
                {
                    return asyncStrategy();
                }

                syncStrategy?.Invoke();

                return ValueTask.CompletedTask;
            }
        }
    }
}
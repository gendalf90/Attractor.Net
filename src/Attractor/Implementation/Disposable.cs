using System;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal static class Disposable
    {
        public static IDisposable Empty { get; } = new EmptyInstance();

        public static IAsyncDisposable EmptyAsync { get; } = new EmptyInstance();
        
        public static IDisposable Create(Action strategy)
        {
            ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

            return new StrategyInstance(strategy, null);
        }

        public static IAsyncDisposable CreateAsync(Func<ValueTask> strategy)
        {
            ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

            return new StrategyInstance(null, strategy);
        }

        private class EmptyInstance : IDisposable, IAsyncDisposable
        {
            void IDisposable.Dispose()
            {
            }

            ValueTask IAsyncDisposable.DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }

        private class StrategyInstance(Action Strategy, Func<ValueTask> AsyncStrategy) : IDisposable, IAsyncDisposable
        {
            ValueTask IAsyncDisposable.DisposeAsync()
            {
                return AsyncStrategy();
            }

            void IDisposable.Dispose()
            {
                Strategy();
            }
        }
    }
}
using System;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal static class Disposable
    {
        public static IDisposable Create(Action strategy)
        {
            ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

            return new Disposing(strategy, null);
        }

        public static IAsyncDisposable CreateAsync(Func<ValueTask> strategy)
        {
            ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

            return new Disposing(null, strategy);
        }

        private record Disposing(Action Strategy, Func<ValueTask> AsyncStrategy) : IDisposable, IAsyncDisposable
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
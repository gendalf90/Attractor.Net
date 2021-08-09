using System;
using System.Threading;
using System.Threading.Tasks;
using Attractor.Implementation.Common;

namespace Attractor.Implementation.Pool
{
    internal sealed class BlockingBoundedActorPool : IActorPool, IAsyncDisposable
    {
        private readonly SemaphoreSlim semaphore;

        private readonly object sync = new object();

        private bool isDisposed;

        public BlockingBoundedActorPool(int limit)
        {
            semaphore = new SemaphoreSlim(limit, limit);
        }

        public async ValueTask<TryResult<IAsyncDisposable>> TryUsePlaceAsync(CancellationToken token = default)
        {
            await semaphore.WaitAsync(token);

            return new TrueResult<IAsyncDisposable>(new StrategyDisposable(() =>
            {
                lock (sync)
                {
                    if (!isDisposed)
                    {
                        semaphore.Release();
                    }
                }
            }));
        }

        public ValueTask DisposeAsync()
        {
            lock (sync)
            {
                isDisposed = true;

                semaphore.Dispose();
            }

            return ValueTaskBuilder.CompletedTask;
        }
    }
}

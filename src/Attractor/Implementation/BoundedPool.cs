using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class BoundedPool : IPool, IDisposable
    {
        private readonly SemaphoreSlim semaphore;
        private readonly TimeSpan timeout;

        public BoundedPool(int capacity, TimeSpan? timeout = null)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            this.timeout = timeout ?? Timeout.InfiniteTimeSpan;

            semaphore = new SemaphoreSlim(capacity, capacity);
        }

        public async ValueTask<IAsyncDisposable> AddAsync(CancellationToken token = default)
        {
            if (!await semaphore.WaitAsync(timeout, token))
            {
                throw new TimeoutException();
            }
            
            return new Disposing(this);
        }

        public void Dispose()
        {
            semaphore.Dispose();
        }

        private class Disposing : IAsyncDisposable
        {
            private const int Initial = 0;
            private const int Disposed = 1;

            private readonly BoundedPool pool;

            private int state = Initial;

            public Disposing(BoundedPool pool)
            {
                this.pool = pool;
            }

            public ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref state, Disposed) == Initial)
                {
                    pool.semaphore.Release();
                }

                return ValueTask.CompletedTask;
            }
        }
    }
}

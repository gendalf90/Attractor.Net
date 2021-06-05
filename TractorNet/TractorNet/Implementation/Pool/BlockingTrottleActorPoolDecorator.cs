using System;
using System.Threading;
using System.Threading.Tasks;

namespace TractorNet.Implementation.Pool
{
    internal sealed class BlockingTrottleActorPoolDecorator : BaseActorPoolDecorator
    {
        private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);
        private readonly TimeSpan period;

        public BlockingTrottleActorPoolDecorator(IActorPool pool, TimeSpan period) : base(pool)
        {
            this.period = period;
        }

        public override async ValueTask<TryResult<IAsyncDisposable>> TryUsePlaceAsync(CancellationToken token = default)
        {
            var result = await pool.TryUsePlaceAsync(token);

            if (!result)
            {
                return result;
            }

            try
            {
                await semaphore.WaitAsync(token);

                _ = Task.Delay(period, token).ContinueWith(_ => semaphore.Release());

                return result;
            }
            catch
            {
                await result.Value.DisposeAsync();

                throw;
            }
        }

        public override ValueTask DisposeAsync()
        {
            semaphore.Dispose();

            return base.DisposeAsync();
        }
    }
}

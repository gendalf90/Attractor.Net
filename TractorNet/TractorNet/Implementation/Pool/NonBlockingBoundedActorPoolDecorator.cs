using System;
using System.Threading;
using System.Threading.Tasks;
using TractorNet.Implementation.Common;

namespace TractorNet.Implementation.Pool
{
    internal sealed class NonBlockingBoundedActorPoolDecorator : BaseActorPoolDecorator
    {
        private readonly int limit;
        
        private int count;

        public NonBlockingBoundedActorPoolDecorator(IActorPool pool, int limit) : base(pool)
        {
            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }

            this.limit = limit;
        }

        public override async ValueTask<TryResult<IAsyncDisposable>> TryUsePlaceAsync(CancellationToken token = default)
        {
            var placeIsUsed = false;

            try
            {
                if (Interlocked.Increment(ref count) > limit)
                {
                    return new FalseResult<IAsyncDisposable>();
                }

                var result = await pool.TryUsePlaceAsync(token);

                placeIsUsed = result;

                return result
                    ? new TrueResult<IAsyncDisposable>(new StrategyDisposable(async () =>
                    {
                        await result.Value.DisposeAsync();

                        Interlocked.Decrement(ref count);
                    }))
                    : result;
            }
            finally
            {
                if (!placeIsUsed)
                {
                    Interlocked.Decrement(ref count);
                }
            }
        }
    }
}

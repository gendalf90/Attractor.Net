using System;
using System.Threading;
using System.Threading.Tasks;
using Attractor.Implementation.Common;

namespace Attractor.Implementation.Pool
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
            var completionDisposing = new StrategyDisposable(() =>
            {
                Interlocked.Decrement(ref count);
            });

            await using (var conditionDisposing = new ConditionDisposable(completionDisposing, true))
            {
                if (Interlocked.Increment(ref count) > limit)
                {
                    return new FalseResult<IAsyncDisposable>();
                }

                var result = await pool.TryUsePlaceAsync(token);

                if (result)
                {
                    conditionDisposing.Disable();
                }

                return result
                    ? new TrueResult<IAsyncDisposable>(new StrategyDisposable(async () =>
                    {
                        await result.Value.DisposeAsync();
                        await completionDisposing.DisposeAsync();
                    }))
                    : result;
            }
        }
    }
}

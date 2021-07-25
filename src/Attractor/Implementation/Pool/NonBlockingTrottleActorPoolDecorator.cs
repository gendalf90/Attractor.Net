using System;
using System.Threading;
using System.Threading.Tasks;
using Attractor.Implementation.Common;

namespace Attractor.Implementation.Pool
{
    internal sealed class NonBlockingTrottleActorPoolDecorator : BaseActorPoolDecorator
    {
        private const int Opened = 0;
        private const int Closed = 1;

        private readonly TimeSpan period;

        private int currentState = Opened;

        public NonBlockingTrottleActorPoolDecorator(IActorPool pool, TimeSpan period) : base(pool)
        {
            this.period = period;
        }

        public override async ValueTask<TryResult<IAsyncDisposable>> TryUsePlaceAsync(CancellationToken token = default)
        {
            if (Interlocked.CompareExchange(ref currentState, Closed, Opened) == Closed)
            {
                return new FalseResult<IAsyncDisposable>();
            }

            var completionDisposing = new StrategyDisposable(() =>
            {
                Interlocked.Exchange(ref currentState, Opened);
            });

            await using (var conditionDisposing = new ConditionDisposable(completionDisposing, true))
            {
                var result = await pool.TryUsePlaceAsync(token);

                if (result)
                {
                    conditionDisposing.Disable();

                    _ = Task.Delay(period, token).ContinueWith(_ => Interlocked.Exchange(ref currentState, Opened));
                }

                return result;
            }
        }
    }
}

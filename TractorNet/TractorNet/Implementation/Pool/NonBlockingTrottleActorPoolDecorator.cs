using System;
using System.Threading;
using System.Threading.Tasks;

namespace TractorNet.Implementation.Pool
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
            var placeIsUsed = false;

            if (Interlocked.CompareExchange(ref currentState, Closed, Opened) == Closed)
            {
                return new FalseResult<IAsyncDisposable>();
            }

            try
            {
                var result = await pool.TryUsePlaceAsync(token);

                placeIsUsed = result;

                if (result)
                {
                    _ = Task.Delay(period, token).ContinueWith(_ => Interlocked.Exchange(ref currentState, Opened));
                }

                return result;
            }
            finally
            {
                if (!placeIsUsed)
                {
                    Interlocked.Exchange(ref currentState, Opened);
                }
            }
        }
    }
}

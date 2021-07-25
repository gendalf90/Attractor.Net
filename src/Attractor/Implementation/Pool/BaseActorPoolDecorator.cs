using System;
using System.Threading;
using System.Threading.Tasks;
using Attractor.Implementation.Common;

namespace Attractor.Implementation.Pool
{
    internal abstract class BaseActorPoolDecorator : IActorPool, IAsyncDisposable
    {
        protected readonly IActorPool pool;

        public BaseActorPoolDecorator(IActorPool pool)
        {
            this.pool = pool;
        }

        public abstract ValueTask<TryResult<IAsyncDisposable>> TryUsePlaceAsync(CancellationToken token = default);

        public virtual ValueTask DisposeAsync()
        {
            return pool.TryDisposeAsync();
        }
    }
}

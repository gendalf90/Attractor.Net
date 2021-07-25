using System;
using System.Threading;
using System.Threading.Tasks;
using Attractor.Implementation.Common;

namespace Attractor.Implementation.Pool
{
    internal sealed class UnboundedActorPool : IActorPool
    {
        public ValueTask<TryResult<IAsyncDisposable>> TryUsePlaceAsync(CancellationToken token = default)
        {
            return ValueTask.FromResult<TryResult<IAsyncDisposable>>(new TrueResult<IAsyncDisposable>(new EmptyDisposable()));
        }
    }
}

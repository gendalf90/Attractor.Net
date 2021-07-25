using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IActorPool
    {
        ValueTask<TryResult<IAsyncDisposable>> TryUsePlaceAsync(CancellationToken token = default);
    }
}

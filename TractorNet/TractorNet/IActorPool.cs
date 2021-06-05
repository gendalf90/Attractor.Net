using System;
using System.Threading;
using System.Threading.Tasks;

namespace TractorNet
{
    public interface IActorPool
    {
        ValueTask<TryResult<IAsyncDisposable>> TryUsePlaceAsync(CancellationToken token = default);
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface ISemaphore
    {
        ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken token = default);
    }
}
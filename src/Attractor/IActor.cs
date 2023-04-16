using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IActor : IAsyncDisposable
    {
        ValueTask OnReceiveAsync(IContext context, CancellationToken token = default);
    }
}
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor;

public interface IActor : IAsyncDisposable
{
    Task StartAsync(IContext context, CancellationToken token = default);

    Task ReceiveAsync(IContext context, CancellationToken token = default);
}

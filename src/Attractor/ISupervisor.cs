using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface ISupervisor
    {
        ValueTask OnStoppedAsync(IContext context, CancellationToken token = default);
        
        ValueTask OnProcessedAsync(IContext context, Exception error, CancellationToken token = default);
    }
}
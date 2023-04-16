using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface ISupervisor
    {
        ValueTask OnFaultAsync(IContext context, Exception exception, CancellationToken token = default);
    }
}
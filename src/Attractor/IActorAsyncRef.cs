using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IActorAsyncRef
    {
        ValueTask SendAsync(IPayload payload, Action<IContext> configuration = null, CancellationToken token = default);
    }
}
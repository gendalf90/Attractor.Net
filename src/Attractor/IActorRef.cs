using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IActorRef
    {
        ValueTask PostAsync(IPayload payload, Action<IContext> configuration = null, CancellationToken token = default);
    }
}

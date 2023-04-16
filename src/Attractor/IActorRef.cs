using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IActorRef
    {
        ValueTask SendAsync(IPayload payload, Action<IContext> configuration = null, CancellationToken token = default); //features - например при inmemory системе delay и ttl вообще не нужны (в mongo пакете будут)
    }
}

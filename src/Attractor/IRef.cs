using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IRef : IAddress, ICancellation, ICompletion
    {
        ValueTask<IRequest> SendAsync(IPayload payload, Action<IContext> configuration = null, CancellationToken token = default); //features - например при inmemory системе delay и ttl вообще не нужны (в mongo пакете будут)
    }
}

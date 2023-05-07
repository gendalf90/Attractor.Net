using System;

namespace Attractor
{
    public interface IActorRef
    {
        void Send(IPayload payload, Action<IContext> configuration = null);
    }
}

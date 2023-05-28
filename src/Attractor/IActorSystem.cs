using System;

namespace Attractor
{
    public interface IActorSystem
    {
        IActorRef Refer(IAddress address);

        void Register(IAddressPolicy policy, Action<IActorBuilder> configuration = null);
    }
}
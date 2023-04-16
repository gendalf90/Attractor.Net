using System;

namespace Attractor
{
    public interface IActorSystem
    {
        IActorRef Ref(IAddress address);

        void Register(IAddressPolicy policy, Action<IActorBuilder> configuration = null);
    }
}
using System;

namespace Attractor
{
    public interface IActorSystem
    {
        IActorRef Refer(IAddress address, bool onlyExist = false);

        void Register(IAddressPolicy policy, Action<IActorBuilder> configuration = null);
    }
}
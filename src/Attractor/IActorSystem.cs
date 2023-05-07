using System;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IActorSystem
    {
        ValueTask<Try<IActorRef>> TryGetRefAsync(IAddress address);

        void Register(IAddressPolicy policy, Action<IActorBuilder> configuration = null);
    }
}
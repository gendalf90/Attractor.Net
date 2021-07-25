using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation.Actor
{
    internal abstract class BaseActorDecorator : IActorDecorator
    {
        protected IActor actor;

        public void Decorate(IActor actor)
        {
            this.actor = actor;
        }

        public abstract ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token = default);
    }
}

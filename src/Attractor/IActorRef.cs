using System.Threading;

namespace Attractor;

public interface IActorRef
{
    IRequest Send(IMessage message, CancellationToken token = default);
}

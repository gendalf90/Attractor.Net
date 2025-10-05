using System.Threading.Tasks;

namespace Attractor;

public interface IActorRef
{
    Task Send(IMessage message);
}

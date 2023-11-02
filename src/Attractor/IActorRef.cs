using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IActorRef
    {
        ValueTask SendAsync(IContext context, CancellationToken token = default);
    }
}

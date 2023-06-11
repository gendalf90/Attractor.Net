using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IActorRef
    {
        ValueTask PostAsync(IContext context, CancellationToken token = default);
    }
}

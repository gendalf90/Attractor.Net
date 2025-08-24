using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IActorRef
    {
        Task SendAsync(IContext context, CancellationToken token = default);
    }
}

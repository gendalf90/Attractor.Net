using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IActorRef
    {
        ValueTask SendAsync(IList context, CancellationToken token = default);
    }
}

using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IActor
    {
        ValueTask OnReceiveAsync(IList context, CancellationToken token = default);
    }
}
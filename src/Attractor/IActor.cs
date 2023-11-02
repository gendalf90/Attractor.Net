using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IActor
    {
        ValueTask OnReceiveAsync(IContext context, CancellationToken token = default);
    }
}
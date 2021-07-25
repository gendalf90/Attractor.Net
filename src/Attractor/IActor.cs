using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IActor
    {
        ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token = default);
    }
}

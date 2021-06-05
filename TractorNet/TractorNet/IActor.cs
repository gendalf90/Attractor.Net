using System.Threading;
using System.Threading.Tasks;

namespace TractorNet
{
    public interface IActor
    {
        ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token = default);
    }
}

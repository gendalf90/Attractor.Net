using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IMailbox
    {
        ValueTask SendAsync(IContext message, CancellationToken token = default);

        ValueTask<IContext> ReceiveAsync(CancellationToken token = default);
    }
}
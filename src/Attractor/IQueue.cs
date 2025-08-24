using System.Threading;
using System.Threading.Tasks;

namespace Attractor;

public interface IQueue
{
    Task SendAsync(IContext[] batch, CancellationToken token = default);

    Task<IContext[]> ReceiveAsync(CancellationToken token = default);
}
using System.Threading;
using System.Threading.Tasks;

namespace Attractor;

public interface IHandler
{
    ValueTask OnReceiveAsync(IContext context, CancellationToken token = default);
}
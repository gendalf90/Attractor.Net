using System.Threading;
using System.Threading.Tasks;

namespace Attractor;

public interface IHandler
{
    Task OnReceive(IContext context, CancellationToken token = default);
}

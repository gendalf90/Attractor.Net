using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public delegate Task DecorateReceive(Receive next, IContext context, CancellationToken token = default);

    public delegate Task Receive(IContext context, CancellationToken token = default);

    public delegate Task Receive<T>(T value, CancellationToken token = default);

    public delegate ValueTask DecorateDispose(Dispose next);

    public delegate ValueTask Dispose();

    public delegate ValueTask<bool> OnMatch(IContext context, CancellationToken token = default);
}

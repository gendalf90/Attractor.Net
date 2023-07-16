using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public delegate ValueTask OnReceiveDecorator(OnReceive next, IContext context, CancellationToken token = default);

    public delegate ValueTask OnReceive(IContext context, CancellationToken token = default);

    public delegate ValueTask OnDisposeDecorator(OnDispose next);

    public delegate ValueTask OnDispose();

    public delegate ValueTask OnCollectDecorator(OnCollect next, IContext context, CancellationToken token = default);

    public delegate ValueTask OnCollect(IContext context, CancellationToken token = default);
}

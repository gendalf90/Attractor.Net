using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public delegate ValueTask OnReceiveDecorator(OnReceive next, IContext context, CancellationToken token = default);

    public delegate ValueTask OnReceive(IContext context, CancellationToken token = default);

    public delegate ValueTask OnReceive<T>(T payload, CancellationToken token = default);

    public delegate ValueTask<bool> OnMatch(IContext context, CancellationToken token = default);
}

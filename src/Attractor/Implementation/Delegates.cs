using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public delegate ValueTask OnReceiveDecorator(OnReceive next, IList context, CancellationToken token = default);

    public delegate ValueTask OnReceive(IList context, CancellationToken token = default);

    public delegate ValueTask OnReceive<T>(T payload, CancellationToken token = default);

    public delegate ValueTask<bool> OnMatch(IList context, CancellationToken token = default);
}

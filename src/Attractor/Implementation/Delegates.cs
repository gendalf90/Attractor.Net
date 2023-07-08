using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public delegate ValueTask OnReceiveDecorator(Func<IContext, CancellationToken, ValueTask> next, IContext context, CancellationToken token = default);

    public delegate ValueTask OnDisposeDecorator(Func<ValueTask> next);

    public delegate ValueTask OnPushDecorator(Func<IContext, ValueTask> next, IContext context);
}

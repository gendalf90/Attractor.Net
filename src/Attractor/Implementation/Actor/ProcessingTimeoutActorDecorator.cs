using System;
using System.Threading;
using System.Threading.Tasks;
using Attractor.Implementation.Common;

namespace Attractor.Implementation.Actor
{
    internal sealed class ProcessingTimeoutActorDecorator : BaseActorDecorator
    {
        private readonly TimeSpan timeout;

        public ProcessingTimeoutActorDecorator(TimeSpan timeout)
        {
            this.timeout = timeout;
        }

        public override async ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token = default)
        {
            await using (token.WithDelay(timeout, out var timeoutToken))
            {
                await actor.OnReceiveAsync(context, timeoutToken);
            }
        }
    }
}

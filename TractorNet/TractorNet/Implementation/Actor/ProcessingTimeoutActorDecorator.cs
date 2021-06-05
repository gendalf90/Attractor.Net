using System;
using System.Threading;
using System.Threading.Tasks;
using TractorNet.Implementation.Common;

namespace TractorNet.Implementation.Actor
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

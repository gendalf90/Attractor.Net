using System;
using System.Threading;
using System.Threading.Tasks;

namespace TractorNet.Implementation.Actor
{
    internal sealed class StrategyActorDecorator : BaseActorDecorator
    {
        private readonly Func<IActor, ReceivedMessageContext, CancellationToken, ValueTask> strategy;

        public StrategyActorDecorator(Func<IActor, ReceivedMessageContext, CancellationToken, ValueTask> strategy)
        {
            this.strategy = strategy;
        }

        public override ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token = default)
        {
            return strategy(actor, context, token);
        }
    }
}

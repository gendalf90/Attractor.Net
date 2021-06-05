using System;
using System.Threading;
using System.Threading.Tasks;

namespace TractorNet.Implementation.Actor
{
    internal sealed class StrategyActor : IActor
    {
        private readonly Func<ReceivedMessageContext, CancellationToken, ValueTask> strategy;

        public StrategyActor(Func<ReceivedMessageContext, CancellationToken, ValueTask> strategy)
        {
            this.strategy = strategy;
        }

        public ValueTask OnReceiveAsync(ReceivedMessageContext context, CancellationToken token = default)
        {
            return strategy(context, token);
        }
    }
}

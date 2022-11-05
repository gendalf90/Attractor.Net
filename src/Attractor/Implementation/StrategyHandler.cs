using System;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class StrategyHandler : IStreamHandler
    {
        private readonly Func<IContext, ValueTask> onStart;
        private readonly Func<IContext, ValueTask> onReceive;

        public StrategyHandler(Func<IContext, ValueTask> onStart, Func<IContext, ValueTask> onReceive)
        {
            this.onStart = onStart;
            this.onReceive = onReceive;
        }

        public ValueTask OnReceiveAsync(IContext context)
        {
            return onStart == null ? ValueTask.CompletedTask : onStart(context);
        }

        public ValueTask OnStartAsync(IContext context)
        {
            return onReceive == null ? ValueTask.CompletedTask : onReceive(context);
        }
    }
}

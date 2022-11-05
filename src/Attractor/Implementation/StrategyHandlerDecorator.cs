using System;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class StrategyHandlerDecorator : BaseHandlerDecorator
    {
        private readonly Func<Func<IContext, ValueTask>, IContext, ValueTask> onStart;
        private readonly Func<Func<IContext, ValueTask>, IContext, ValueTask> onReceive;

        public StrategyHandlerDecorator(Func<Func<IContext, ValueTask>, IContext, ValueTask> onStart, Func<Func<IContext, ValueTask>, IContext, ValueTask> onReceive)
        {
            this.onStart = onStart;
            this.onReceive = onReceive;
        }

        public override ValueTask OnStartAsync(IContext context)
        {
            return onStart == null ? base.OnStartAsync(context) : onStart(base.OnStartAsync, context);
        }

        public override ValueTask OnReceiveAsync(IContext context)
        {
            return onReceive == null ? base.OnReceiveAsync(context) : onReceive(base.OnReceiveAsync, context);
        }
    }
}

using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class ChainHandler : BaseHandlerDecorator
    {
        private readonly IStreamHandler handler;

        public ChainHandler(IStreamHandler handler)
        {
            this.handler = handler;
        }

        public override async ValueTask OnStartAsync(IContext context)
        {
            await base.OnStartAsync(context);

            await handler.OnStartAsync(context);
        }

        public override async ValueTask OnReceiveAsync(IContext context)
        {
            await base.OnReceiveAsync(context);

            await handler.OnReceiveAsync(context);
        }
    }
}

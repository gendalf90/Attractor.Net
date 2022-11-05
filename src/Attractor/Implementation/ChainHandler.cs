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
            await handler.OnStartAsync(context);

            await base.OnStartAsync(context);
        }

        public override async ValueTask OnReceiveAsync(IContext context)
        {
            await handler.OnReceiveAsync(context);

            await base.OnReceiveAsync(context);
        }
    }
}

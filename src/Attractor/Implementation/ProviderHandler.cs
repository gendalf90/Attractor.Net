using System;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class ProviderHandler : BaseHandlerDecorator
    {
        private readonly IServiceProvider provider;

        public ProviderHandler(IServiceProvider provider)
        {
            this.provider = provider;
        }

        public override ValueTask OnStartAsync(IContext context)
        {
            context.Set(provider);

            return base.OnStartAsync(context);
        }

        public override ValueTask OnReceiveAsync(IContext context)
        {
            context.Set(provider);

            return base.OnReceiveAsync(context);
        }
    }
}

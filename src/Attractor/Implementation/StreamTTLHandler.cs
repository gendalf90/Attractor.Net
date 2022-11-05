using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class StreamTTLHandler : BaseHandlerDecorator
    {
        private readonly TimeSpan ttl;

        public StreamTTLHandler(TimeSpan ttl)
        {
            this.ttl = ttl;
        }

        public override async ValueTask OnStartAsync(IContext context)
        {
            var selfRef = context.Get<IRef>();
            var cancellation = new CancellationTokenSource(ttl);
            
            cancellation.Token.Register(selfRef.Cancel);
            selfRef.GetToken().Register(cancellation.Dispose);

            await base.OnStartAsync(context);
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class RequestTTLHandler : BaseHandlerDecorator
    {
        private readonly TimeSpan ttl;

        public RequestTTLHandler(TimeSpan ttl)
        {
            this.ttl = ttl;
        }

        public override async ValueTask OnReceiveAsync(IContext context)
        {
            var request = context.Get<IRequest>();
            var cancellation = new CancellationTokenSource(ttl);
            
            cancellation.Token.Register(request.Cancel);
            request
                .WaitAsync()
                .GetAwaiter()
                .OnCompleted(cancellation.Dispose);

            await base.OnReceiveAsync(context);
        }
    }
}

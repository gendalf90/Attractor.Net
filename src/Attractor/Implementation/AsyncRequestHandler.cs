using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class AsyncRequestHandler : BaseHandlerDecorator
    {
        public override async ValueTask OnReceiveAsync(IContext context)
        {
            var asyncToken = context.Get<IAsyncToken>();

            if (asyncToken == null)
            {
                var completionSource = context.Get<ICompletionTokenSource>();
                var token = completionSource.Attach();
                var clonedContext = context.Clone();
                var newCompletionSource = new CompletionTokenSource();

                newCompletionSource
                    .WaitAsync()
                    .GetAwaiter()
                    .OnCompleted(token.Dispose);

                clonedContext.Set<IAsyncToken>(new AsyncToken(newCompletionSource));
                clonedContext.Set<ICompletionTokenSource>(newCompletionSource);

                await base.OnReceiveAsync(clonedContext);
            }
            else
            {
                context.Set<IAsyncToken>(null);

                using (asyncToken)
                {
                    await base.OnReceiveAsync(context);
                }
            }
        }

        private class AsyncToken : IAsyncToken
        {
            private readonly CompletionTokenSource source;

            public AsyncToken(CompletionTokenSource source)
            {
                this.source = source;
            }

            public void Dispose()
            {
                source.Dispose();
            }
        }
    }
}

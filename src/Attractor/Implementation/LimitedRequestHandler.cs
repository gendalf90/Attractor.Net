using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class LimitedRequestHandler : BaseHandlerDecorator
    {
        private readonly TaskCompletionSource completion = new TaskCompletionSource();

        private readonly int limit;

        private int processed;
        private int initial;

        public LimitedRequestHandler(int limit)
        {
            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }

            this.limit = limit;
        }

        public override async ValueTask OnReceiveAsync(IContext context)
        {
            var request = context.Get<IRequest>();

            if (Interlocked.Increment(ref initial) > limit)
            {
                await completion.Task.WaitAsync(request.GetToken());
            }

            var selfRef = context.Get<IRef>();
            var completionSource = context.Get<ICompletionTokenSource>();

            completionSource.Register(() =>
            {
                if (Interlocked.Increment(ref processed) != limit)
                {
                    return ValueTask.CompletedTask;
                }

                selfRef.Cancel();
                completion.SetCanceled(selfRef.GetToken());

                return ValueTask.CompletedTask;
            });

            await base.OnReceiveAsync(context);
        }
    }
}

using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class RequestPoolHandler : BaseHandlerDecorator
    {
        private readonly IPool pool;

        public RequestPoolHandler(IPool pool)
        {
            this.pool = pool;
        }

        public override async ValueTask OnReceiveAsync(IContext context)
        {
            var request = context.Get<IRequest>();

            var poolDisposing = await pool.AddAsync(request.GetToken());

            var completionSource = context.Get<ICompletionTokenSource>();

            completionSource.Register(poolDisposing.DisposeAsync);

            await base.OnReceiveAsync(context);
        }
    }
}

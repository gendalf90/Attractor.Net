using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class StreamPoolHandler : BaseHandlerDecorator
    {
        private readonly IPool pool;

        public StreamPoolHandler(IPool pool)
        {
            this.pool = pool;
        }

        public override async ValueTask OnStartAsync(IContext context)
        {
            var request = context.Get<IRequest>();

            var poolDisposing = await pool.AddAsync(request.GetToken());

            var completionSource = context.Get<ICompletionTokenSource>();

            completionSource.Register(poolDisposing.DisposeAsync);

            await base.OnStartAsync(context);
        }
    }
}

using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal abstract class BaseHandlerDecorator : IStreamHandlerDecorator
    {
        protected IStreamHandler decorated;

        public void Decorate(IStreamHandler handler)
        {
            decorated = handler;
        }

        public virtual ValueTask OnStartAsync(IContext context)
        {
            return decorated == null ? ValueTask.CompletedTask : decorated.OnStartAsync(context);
        }

        public virtual ValueTask OnReceiveAsync(IContext context)
        {
            return decorated == null ? ValueTask.CompletedTask : decorated.OnReceiveAsync(context);
        }
    }
}

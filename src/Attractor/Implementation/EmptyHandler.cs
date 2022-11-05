using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class EmptyHandler : IStreamHandler
    {
        public ValueTask OnReceiveAsync(IContext context)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask OnStartAsync(IContext context)
        {
            return ValueTask.CompletedTask;
        }
    }
}

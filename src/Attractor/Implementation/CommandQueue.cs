using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class CommandQueue : Dispatcher
    {
        //private readonly ConcurrentQueue<ICommand> queue = new();
        private readonly LinkedQueue<ICommand> queue = new();

        public void Schedule(ICommand command)
        {
            queue.Enqueue(command);

            Touch();
        }
        
        protected override async ValueTask ProcessAsync()
        {
            while (queue.TryDequeue(out var command))
            {
                try
                {
                    await command.ExecuteAsync();
                }
                catch
                {
                    continue;
                }
            }
        }
    }
}
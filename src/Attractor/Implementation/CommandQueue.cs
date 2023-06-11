using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class CommandQueue<T> : Dispatcher where T : ICommand
    {
        private readonly ConcurrentQueue<T> queue = new();

        public void Schedule(T command)
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
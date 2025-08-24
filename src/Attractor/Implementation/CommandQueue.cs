using System.Collections.Concurrent;

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
        
        protected override void Process()
        {
            while (queue.TryDequeue(out var command))
            {
                try
                {
                    command.Execute();
                }
                catch
                {
                    continue;
                }
            }
        }
    }
}
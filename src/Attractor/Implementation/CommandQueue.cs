using System.Collections.Concurrent;

namespace Attractor.Implementation
{
    internal class CommandQueue : Dispatcher, ICommandQueue
    {
        private readonly ConcurrentQueue<ICommand> queue = new();

        public void Schedule(ICommand command)
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
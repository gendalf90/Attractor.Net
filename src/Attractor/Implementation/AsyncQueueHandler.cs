using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class AsyncQueueHandler : BaseHandlerDecorator, IThreadPoolWorkItem
    {
        private const int Unlocked = 0;
        private const int Locked = 1;

        private readonly ConcurrentQueue<IContext> events = new();

        private int state = Unlocked;

        public void Execute()
        {
            _ = StartEventProcessingAsync();
        }

        public override ValueTask OnReceiveAsync(IContext context)
        {
            events.Enqueue(context);

            if (IsNotLocked())
            {
                ThreadPool.UnsafeQueueUserWorkItem(this, true);
            }
            
            return ValueTask.CompletedTask;
        }

        private async ValueTask StartEventProcessingAsync()
        {
            while (!events.IsEmpty)
            {
                if (!TryLock())
                {
                    return;
                }

                try
                {
                    while (events.TryDequeue(out var context))
                    {
                        await decorated.OnReceiveAsync(context);
                    }
                }
                catch
                {
                    continue;
                }
                finally
                {
                    Unlock();
                }
            }
        }

        private bool TryLock()
        {
            return Interlocked.CompareExchange(ref state, Locked, Unlocked) == Unlocked;
        }

        private void Unlock()
        {
            Interlocked.Exchange(ref state, Unlocked);
        }

        private bool IsNotLocked()
        {
            return Interlocked.Add(ref state, 0) == Unlocked;
        }
    }
}

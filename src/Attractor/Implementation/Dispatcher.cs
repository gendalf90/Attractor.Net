using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal abstract class Dispatcher : IThreadPoolWorkItem
    {
        private const long LockValue = 1;
        private const long UnlockValue = 0;

        private long counter = UnlockValue;

        async void IThreadPoolWorkItem.Execute()
        {
            ResetLock();
            
            try
            {
                await ProcessAsync();
            }
            catch 
            {
                // do nothing
            }
            finally
            {
                Unlock();
            }
        }

        private void Unlock()
        {
            if (!TryUnlock())
            {
                ThreadPool.UnsafeQueueUserWorkItem(this, true);
            }
        }

        // void IThreadPoolWorkItem.Execute()
        // {
        //     StartProcessingAsync().GetAwaiter().UnsafeOnCompleted(() =>
        //     {
        //         if (!TryUnlock())
        //         {
        //             ThreadPool.UnsafeQueueUserWorkItem(this, true);
        //         }
        //     });
        // }

        public void Touch()
        {
            if (TryLock())
            {
                ThreadPool.UnsafeQueueUserWorkItem(this, true);
            }
        }

        protected abstract ValueTask ProcessAsync();

        // private async ValueTask StartProcessingAsync()
        // {
        //     ResetLock();
            
        //     try
        //     {
        //         await ProcessAsync();
        //     }
        //     finally
        //     {
        //         if (!TryUnlock())
        //         {
        //             ThreadPool.UnsafeQueueUserWorkItem(this, true);
        //         }
        //     }
        // }

        // private async ValueTask StartProcessingAsync()
        // {
        //     ResetLock();

        //     await ProcessAsync();
        // }

        private bool TryLock()
        {
            return Interlocked.Increment(ref counter) == LockValue;
        }

        private bool TryUnlock()
        {
            return Interlocked.Decrement(ref counter) == UnlockValue;
        }

        private void ResetLock()
        {
            Interlocked.Exchange(ref counter, LockValue);
        }
    }
}
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class SpinSemaphore : ISemaphore
    {
        //private readonly ConcurrentQueue<TaskCompletionSource<IAsyncDisposable>> completions = new();
        private readonly LinkedQueue<TaskCompletionSource<IAsyncDisposable>> completions = new();
        private readonly long limit;

        private long counter;

        public SpinSemaphore(long limit)
        {
            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit));
            }

            this.limit = limit;
        }

        async ValueTask<IAsyncDisposable> ISemaphore.AcquireAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            
            if (Interlocked.Increment(ref counter) <= limit)
            {
                return new Releaser(this);
            }

            var completion = new TaskCompletionSource<IAsyncDisposable>();

            completions.Enqueue(completion);

            try
            {
                return await completion.Task.WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                Release();

                throw;
            }
        }

        private void Release()
        {
            if (Interlocked.Decrement(ref counter) < limit)
            {
                return;
            }

            bool received;
            TaskCompletionSource<IAsyncDisposable> completion;

            do
            {
                received = completions.TryDequeue(out completion);
            }
            while (!received);

            completion.SetResult(new Releaser(this));
        }

        private class Releaser : IAsyncDisposable
        {
            private const int InitialState = 0;
            private const int ReleasedState = 1;

            private int state = InitialState;
            
            private readonly SpinSemaphore semaphore;

            public Releaser(SpinSemaphore semaphore)
            {
                this.semaphore = semaphore;
            }

            ValueTask IAsyncDisposable.DisposeAsync()
            {
                if (Interlocked.Exchange(ref state, ReleasedState) == InitialState)
                {
                    semaphore.Release();
                }

                return ValueTask.CompletedTask;
            }
        }
    }
}
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class ReaderLock : IDisposable, IAsyncDisposable
    {
        private const int InitialState = 0;
        private const int CompletionState = 1;
        private const int CompletedState = 2;
        private const int DisposedState = 3;

        private TaskCompletionSource completionSource = new TaskCompletionSource();

        private uint locksCount;
        private int state;
        
        public IDisposable Lock(out bool isAcquired)
        {
            isAcquired = false;

            if (Interlocked.CompareExchange(ref state, DisposedState, CompletedState) != InitialState)
            {
                return Disposable.Empty;
            }

            Interlocked.Increment(ref locksCount);

            isAcquired = Interlocked.CompareExchange(ref state, DisposedState, CompletedState) == InitialState;

            return new Disposing(this);
        }

        private void Release()
        {
            if (Interlocked.Decrement(ref locksCount) > 0)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref state, CompletedState, CompletionState) == CompletionState)
            {
                completionSource.SetResult();
            }
        }

        public Task WaitAsync(CancellationToken token = default)
        {
            return completionSource.Task.WaitAsync(token);
        }

        public async ValueTask DisposeAsync()
        {
            Dispose();

            await WaitAsync();
        }

        public void Dispose()
        {
            using (Lock(out _))
            {
                Interlocked.CompareExchange(ref state, CompletionState, InitialState);
            }
        }

        private class Disposing : IDisposable
        {
            private readonly ReaderLock readerLock;

            private int currentState = InitialState;

            public Disposing(ReaderLock readerLock)
            {
                this.readerLock = readerLock;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref currentState, DisposedState) == InitialState)
                {
                    readerLock.Release();
                }
            }
        }
    }
}

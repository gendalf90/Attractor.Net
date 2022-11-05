using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class CompletionTokenSource : ICompletionTokenSource, ICompletion, IDisposable
    {
        private readonly ReaderLock sourceLock = new ReaderLock();
        private readonly ReaderLock tokensLock = new ReaderLock();

        public IDisposable Attach()
        {
            var sourceLockDisposing = sourceLock.Lock(out var isSourceLockAcquired);
            var tokensLockDisposing = tokensLock.Lock(out var isTokensLockAcquired);

            if (isSourceLockAcquired && isTokensLockAcquired)
            {
                return Disposable.Create(() =>
                {
                    tokensLockDisposing.Dispose();
                    sourceLockDisposing.Dispose();
                });
            }

            sourceLockDisposing.Dispose();
            tokensLockDisposing.Dispose();

            throw new ObjectDisposedException(nameof(CompletionTokenSource));
        }

        public void Register(Func<ValueTask> callback)
        {
            ArgumentNullException.ThrowIfNull(callback, nameof(callback));

            var sourceLockDisposing = sourceLock.Lock(out var isSourceLockAcquired);

            if (isSourceLockAcquired)
            {
                tokensLock
                    .WaitAsync()
                    .GetAwaiter()
                    .OnCompleted(() =>
                    {
                        callback()
                            .GetAwaiter()
                            .OnCompleted(sourceLockDisposing.Dispose);
                    });

                return;
            }

            sourceLockDisposing.Dispose();

            throw new ObjectDisposedException(nameof(CompletionTokenSource));
        }

        public Task WaitAsync(CancellationToken token = default)
        {
            return sourceLock.WaitAsync(token);
        }

        public void Dispose()
        {
            tokensLock.Dispose();
            sourceLock.Dispose();
        }
    }
}

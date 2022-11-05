using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    internal sealed class ThrottlePool : IPool, IDisposable
    {
        private readonly object sync = new object();
        private readonly LinkedList<ThrottleTask> throttledTasks = new LinkedList<ThrottleTask>();

        private readonly int unthrottledCapacity;
        private readonly TimeSpan throttleTime;
        private readonly TimeSpan? timeout;

        private int count;
        private bool isDisposed;

        public ThrottlePool(TimeSpan throttleTime, int? unthrottledCapacity = null, TimeSpan? timeout = null)
        {
            if (unthrottledCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unthrottledCapacity));
            }

            this.unthrottledCapacity = unthrottledCapacity ?? 0;
            this.throttleTime = throttleTime;
            this.timeout = timeout;
        }

        public async ValueTask<IAsyncDisposable> AddAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            await ThrottleTask.WaitAsync(this, token);

            return new Disposing(this);
        }

        private void Release()
        {
            ThrottleTask task = null;

            lock (sync)
            {
                if (isDisposed)
                {
                    return;
                }

                if (--count >= unthrottledCapacity)
                {
                    return;
                }

                if (throttledTasks.Count == 0)
                {
                    return;
                }

                task = throttledTasks.First.Value;

                if (!task.TryClose(true))
                {
                    return;
                }
            }

            task.SetResult();
        }

        public void Dispose()
        {
            List<ThrottleTask> tasks = null;

            lock (sync)
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;

                tasks = throttledTasks.ToList();

                tasks.ForEach(task => task.TryClose(false, false));
            }

            tasks.ForEach(task => task.SetResult(new ObjectDisposedException(nameof(ThrottlePool))));
        }

        private class ThrottleTask : IThreadPoolWorkItem, IDisposable
        {
            private readonly ThrottlePool pool;

            private TaskCompletionSource completionSource;
            private CancellationTokenSource timeoutSource;
            private CancellationTokenSource cancellationSource;
            private LinkedListNode<ThrottleTask> node;
            
            private bool isStarted;
            private bool isClosed;

            private ThrottleTask(ThrottlePool pool)
            {
                this.pool = pool;
            }

            public static async ValueTask WaitAsync(ThrottlePool pool, CancellationToken token)
            {
                using (var task = new ThrottleTask(pool))
                {
                    await task.StartWaitingAsync(token);
                }
            }

            private async ValueTask StartWaitingAsync(CancellationToken token)
            {
                completionSource = new TaskCompletionSource();

                if (pool.timeout.HasValue)
                {
                    timeoutSource = new CancellationTokenSource(pool.timeout.Value);
                }

                cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutSource?.Token ?? default);
                cancellationSource.Token.Register(() => Complete(new OperationCanceledException()));

                lock (pool.sync)
                {
                    if (pool.isDisposed)
                    {
                        throw new ObjectDisposedException(nameof(ThrottlePool));
                    }

                    if (pool.count < pool.unthrottledCapacity)
                    {
                        pool.count++;

                        return;
                    }

                    node = pool.throttledTasks.AddLast(this);

                    pool.throttledTasks.First.Value.TryStart();
                }
                
                await completionSource.Task;
            }

            private void TryStart()
            {
                if (isStarted)
                {
                    return;
                }

                isStarted = true;

                ThreadPool.UnsafeQueueUserWorkItem(this, true);
            }

            public async Task ThrottleAsync()
            {
                try
                {
                    await Task.Delay(pool.throttleTime, cancellationSource.Token);

                    Complete();
                }
                catch (Exception e)
                {
                    Complete(e);
                }
            }

            public bool TryClose(bool success, bool needStartNext = true)
            {
                if (isClosed)
                {
                    return false;
                }

                if (success)
                {
                    pool.count++;
                }

                pool.throttledTasks.Remove(node);

                if (needStartNext)
                {
                    pool.throttledTasks.First?.Value.TryStart();
                }
                
                isClosed = true;

                return true;
            }

            public void SetResult(Exception error = null)
            {
                if (error == null)
                {
                    completionSource.TrySetResult();
                }
                else
                {
                    completionSource.TrySetException(error);
                }

                cancellationSource.Cancel();
            }

            public void Complete(Exception error = null)
            {
                lock (pool.sync)
                {
                    if (!TryClose(error == null))
                    {
                        return;
                    }
                }

                SetResult(error);
            }

            public void Execute()
            {
                _ = ThrottleAsync();
            }

            public void Dispose()
            {
                cancellationSource?.Dispose();
                timeoutSource?.Dispose();
            }
        }

        private class Disposing : IAsyncDisposable
        {
            private static int NotDisposedState = 0;
            private static int DisposedState = 1;

            private readonly ThrottlePool pool;

            private int currentState = NotDisposedState;

            public Disposing(ThrottlePool pool)
            {
                this.pool = pool;
            }

            public ValueTask DisposeAsync()
            {
                if (Interlocked.CompareExchange(ref currentState, DisposedState, NotDisposedState) == NotDisposedState)
                {
                    pool.Release();
                }

                return ValueTask.CompletedTask;
            }
        }
    }
}

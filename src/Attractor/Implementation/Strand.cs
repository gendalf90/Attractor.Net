using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation;

internal class Strand
{
    private readonly StrandingSynchronizationContext context = new();

    public Task<T> Run<T>(Func<Task<T>> func)
    {
        var completion = new TaskCompletionSource<Task<T>>();

        context.Post(_ =>
        {
            try
            {
                completion.SetResult(func());
            }
            catch (OperationCanceledException e)
            {
                completion.SetCanceled(e.CancellationToken);
            }
            catch (Exception e)
            {
                completion.SetException(e);
            }
        }, null);

        return completion.Task.Unwrap();
    }

    public Task<T> Run<T>(Func<T> func)
    {
        var completion = new TaskCompletionSource<T>();

        context.Post(_ =>
        {
            try
            {
                completion.SetResult(func());
            }
            catch (OperationCanceledException e)
            {
                completion.SetCanceled(e.CancellationToken);
            }
            catch (Exception e)
            {
                completion.SetException(e);
            }
        }, null);

        return completion.Task;
    }

    public Task Run(Func<Task> func)
    {
        var completion = new TaskCompletionSource<Task>();

        context.Post(_ =>
        {
            try
            {
                completion.SetResult(func());
            }
            catch (OperationCanceledException e)
            {
                completion.SetCanceled(e.CancellationToken);
            }
            catch (Exception e)
            {
                completion.SetException(e);
            }
        }, null);

        return completion.Task.Unwrap();
    }

    public Task Run(Action func)
    {
        var completion = new TaskCompletionSource();

        context.Post(_ =>
        {
            try
            {
                func();
                completion.SetResult();
            }
            catch (OperationCanceledException e)
            {
                completion.SetCanceled(e.CancellationToken);
            }
            catch (Exception e)
            {
                completion.SetException(e);
            }
        }, null);

        return completion.Task;
    }

    public void Post(Action func)
    {
        context.Post(_ => func(), null);
    }

    private class StrandingSynchronizationContext : SynchronizationContext
    {
        private const long LockValue = 1;
        private const long UnlockValue = 0;

        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object State)> queue = new();
        private readonly WaitCallback execute;
        private long counter = UnlockValue;

        public StrandingSynchronizationContext()
        {
            execute = new WaitCallback(Execute);
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            queue.Enqueue((d, state));

            Touch();
        }

        public override void Send(SendOrPostCallback d, object state)
        {
            var completion = new TaskCompletionSource();

            Post(obj =>
            {
                try
                {
                    d(obj);
                    completion.SetResult();
                }
                catch (OperationCanceledException e)
                {
                    completion.SetCanceled(e.CancellationToken);
                }
                catch (Exception e)
                {
                    completion.SetException(e);
                }
            }, state);

            completion.Task.GetAwaiter().GetResult();
        }

        private void Execute(object state)
        {
            ResetLock();

            try
            {
                Process();
            }
            catch
            {
                return;
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
                ThreadPool.QueueUserWorkItem(execute);
            }
        }

        public void Touch()
        {
            if (TryLock())
            {
                ThreadPool.QueueUserWorkItem(execute);
            }
        }

        private void Process()
        {
            SetSynchronizationContext(this);
            
            while (queue.TryDequeue(out var command))
            {
                try
                {
                    command.Callback(command.State);
                }
                catch
                {
                    continue;
                }
            }
        }

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

using System.Threading;

namespace Attractor.Implementation;

internal abstract class Dispatcher
{
    private const long LockValue = 1;
    private const long UnlockValue = 0;

    private long counter = UnlockValue;

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
            ThreadPool.QueueUserWorkItem(Execute);
        }
    }

    public void Touch()
    {
        if (TryLock())
        {
            ThreadPool.QueueUserWorkItem(Execute);
        }
    }

    protected abstract void Process();

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

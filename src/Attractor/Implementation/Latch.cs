namespace Attractor.Implementation;

internal class Latch(Strand strand)
{
    private readonly LinkedList<Waiter> waiters = new();

    public Task<IDisposable> Use(CancellationToken token)
    {
        return strand.Run(() =>
        {
            token.ThrowIfCancellationRequested();

            var node = waiters.AddLast(new Waiter());

            node.Value.Completion = new TaskCompletionSource<IDisposable>();
            node.Value.Cancellation = token.Register(() => CancelWaiter(node));

            if (waiters.Count == 1)
            {
                node.Value.Completion.SetResult(Disposable.Create(() => DisposeWaiter(node)));
            }

            return node.Value.Completion.Task;
        });
    }

    private void DisposeWaiter(LinkedListNode<Waiter> node)
    {
        strand.Post(() =>
        {
            if (node.List == null)
            {
                return;
            }

            waiters.Remove(node);
            node.Value.Cancellation.Dispose();

            if (waiters.Count > 0)
            {
                waiters.First.Value.Completion.SetResult(Disposable.Create(() => DisposeWaiter(waiters.First)));
            }
        });
    }

    private void CancelWaiter(LinkedListNode<Waiter> node)
    {
        strand.Post(() =>
        {
            if (waiters.Count == 0)
            {
                return;
            }

            if (waiters.First == node)
            {
                return;
            }

            if (node.List != null)
            {
                waiters.Remove(node);
                node.Value.Completion.SetCanceled(node.Value.Cancellation.Token);
                node.Value.Cancellation.Dispose();
            }
        });
    }

    private class Waiter
    {
        public TaskCompletionSource<IDisposable> Completion { get; set; }

        public CancellationTokenRegistration Cancellation { get; set; }
    }
}

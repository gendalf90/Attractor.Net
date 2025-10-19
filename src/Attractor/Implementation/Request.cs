using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation;

internal class Request(params CancellationToken[] tokens) : IRequest
{
    private readonly CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(tokens);
    private readonly TaskCompletionSource completion = new();
    
    public Task Completion => completion.Task;

    public CancellationToken Cancellation => cancellation.Token;

    public void Complete()
    {
        completion.SetResult();
    }

    public void Cancel()
    {
        cancellation.Cancel();
        completion.SetCanceled();
    }

    public void Fault(Exception ex)
    {
        completion.SetException(ex);
    }
}
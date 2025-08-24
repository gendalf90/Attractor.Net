using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation;

internal sealed class Process : IProcess, IAsyncDisposable
{
    private readonly CommandQueue<ICommand> commands = new();

    private readonly IActor actor;
    private readonly CancellationTokenSource cancellation;
    private readonly CancellationTokenRegistration registration;
    private readonly IAsyncDisposable disposing;

    private Task processingTask;
    private Task disposingTask;
    private IContext context;

    public Process(IActor actor, IAsyncDisposable disposing, CancellationToken token)
    {
        this.actor = actor;
        this.disposing = disposing;

        cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        registration = cancellation.Token.Register(OnCancel);
    }

    public Task<OfferStatus> OfferMessageAsync(IContext context)
    {
        var completion = new TaskCompletionSource<OfferStatus>();

        commands.Schedule(Command.Create(() =>
        {
            if (cancellation.IsCancellationRequested)
            {
                completion.SetResult(OfferStatus.DecliningPermanently);
            }
            else if (processingTask == null || processingTask.IsCompletedSuccessfully)
            {
                context = new ProcessContext(context, this);
                processingTask = Task.Run(RunProcessing);

                completion.SetResult(OfferStatus.Accepted);
            }
            else if (processingTask.IsCanceled || processingTask.IsFaulted)
            {
                completion.SetResult(OfferStatus.DecliningPermanently);
            }

            completion.SetResult(OfferStatus.Declined);
        }));

        return completion.Task;
    }

    public void Cancel()
    {
        cancellation.Cancel();
    }

    private void OnCancel()
    {
        commands.Schedule(Command.Create(() =>
        {
            if (disposingTask == null)
            {
                disposingTask = Task.Run(RunDisposingAsync);
            }
        }));
    }

    private async Task RunProcessing()
    {
        try
        {
            await actor.OnReceiveAsync(context, cancellation.Token);
        }
        catch
        {
            cancellation.Cancel();
        }
    }

    private async Task RunDisposingAsync()
    {
        using (cancellation)
        using (registration)
        await using (disposing)
        await using (actor)
        {
            await cancellation.CancelAsync();
            
            if (processingTask != null)
            {
                await processingTask;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        var completion = new TaskCompletionSource();

        commands.Schedule(Command.Create(() =>
        {
            if (disposingTask == null)
            {
                disposingTask = Task.Run(RunDisposingAsync);
            }

            completion.SetResult();
        }));

        await completion.Task;
        await disposingTask;
    }

    private record ProcessContext(IContext Context, Process Process) : IContext
    {
        T IContext.Get<T>()
        {
            if (typeof(T) == typeof(IProcess))
            {
                return Process as T;
            }

            return Context.Get<T>();
        }
    }
}
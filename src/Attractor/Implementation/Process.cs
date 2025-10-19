using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation;

internal class Process(IActor actor, CancellationToken cancellation) : IActorProcess
{
    private readonly CommandQueue commands = new();
    private readonly Queue<ICommand> requests = new();
    private readonly TaskCompletionSource completion = new();

    private State state = State.Initial;
    private CancellationTokenRegistration registration;
    private Task processingTask;
    private IContext processContext;
    private bool isCancelled;
    private Exception error;

    public void Start()
    {
        commands.Schedule(ProcessStartCommand);
    }

    Task ICompletion.Completion => completion.Task;

    CancellationToken ICancellation.Cancellation => cancellation;

    private ICommand ProcessStartCommand => Command.From(() =>
    {
        if (state == State.Initial)
        {
            registration = cancellation.Register(() => commands.Schedule(RunDisposeCommand(isCancelled: true)));
            state = State.Processing;
            processContext = Context.Value<IActorProcess>(this);
            processingTask = Task.Run(() => ProcessStartAsync(processContext));
        }
    });

    IRequest IActorRef.Send(IMessage message, CancellationToken token)
    {
        var request = new Request(token, cancellation);

        Send(Context.From(builder =>
        {
            message.Configure(builder);
            builder.Set(request);
            builder.Set<IRequest>(request);
            builder.Set(message);
        }));

        return request;
    }

    private void Send(IContext context)
    {
        commands.Schedule(ProcessRequestCommand(context));
    }

    private async Task ProcessStartAsync(IContext context)
    {
        try
        {
            await actor.StartAsync(context, cancellation);

            commands.Schedule(ProcessNextRequestCommand);
        }
        catch (Exception ex)
        {
            commands.Schedule(RunDisposeCommand(error: ex));
        }
    }

    private async Task ProcessRequestAsync(IContext context)
    {
        var request = context.Get<Request>();

        try
        {
            await actor.ReceiveAsync(context.With(processContext), request.Cancellation);

            request.Complete();
        }
        catch (OperationCanceledException)
        {
            request.Cancel();
        }
        catch (Exception e)
        {
            request.Fault(e);
        }
        finally
        {
            commands.Schedule(ProcessNextRequestCommand);
        }
    }

    private ICommand ProcessRequestCommand(IContext context) => Command.From(() =>
    {
        var request = context.Get<Request>();

        if (state == State.Disposing)
        {
            request.Cancel();
        }
        else if (state == State.Disposed)
        {
            request.Fault(new ObjectDisposedException(nameof(IActorProcess)));
        }
        else if (state == State.Processing)
        {
            requests.Enqueue(ProcessRequestCommand(context));
        }
        else if (state == State.Started)
        {
            state = State.Processing;
            processingTask = Task.Run(() => ProcessRequestAsync(context));
        }
    });

    private ICommand ProcessNextRequestCommand => Command.From(() =>
    {
        if (state == State.Processing)
        {
            state = State.Started;
        }

        if (state >= State.Disposing)
        {
            return;
        }

        if (requests.TryDequeue(out var command))
        {
            command.Execute();
        }
    });

    private ICommand RunCompleteCommand => Command.From(() =>
    {
        state = State.Disposed;

        if (isCancelled)
        {
            completion.SetCanceled(cancellation);
        }
        else if (error != null)
        {
            completion.SetException(error);
        }
        else
        {
            completion.SetResult();
        }
    });

    private ICommand RunDisposeCommand(bool isCancelled = false, Exception error = null) => Command.From(() =>
    {
        if (state >= State.Disposing)
        {
            return;
        }

        state = State.Disposing;

        this.isCancelled = isCancelled;
        this.error = error;

        while (requests.TryDequeue(out var command))
        {
            command.Execute();
        }

        Task.Run(DisposeInternalAsync);
    });

    private async Task DisposeInternalAsync()
    {
        using (Disposable.Create(Complete))
        using (registration)
        await using (actor)
        {
            if (processingTask != null)
            {
                await processingTask;
            }
        }
    }

    private void Complete()
    {
        commands.ScheduleAsync(RunCompleteCommand);
    }

    public void Dispose()
    {
        commands.ScheduleAsync(RunDisposeCommand());
    }
}
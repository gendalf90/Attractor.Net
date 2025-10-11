using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation;

internal class Process(IActor actor, CancellationToken token) : IActorProcess
{
    private readonly Guid pid = Guid.NewGuid();
    private readonly CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
    private readonly CommandQueue commands = new();
    private readonly Queue<ICommand> requests = new();

    private State state = State.Initial;
    private IDisposable processCompletion = Disposable.Empty;
    private IDisposable processCancellation = Disposable.Empty;
    private CancellationTokenRegistration registration;
    private Task processingTask;
    private IContext processContext;
    private IDisposable completion;

    public void Start()
    {
        commands.Schedule(ProcessStartCommand);
    }

    public Guid PID => pid;

    private ICommand ProcessStartCommand => Command.From(() =>
    {
        if (state == State.Initial)
        {
            registration = cancellation.Token.Register(Dispose);
            completion = Disposable.Create(CompleteDispose);
            state = State.Processing;
            processContext = Context.Value<IActorProcess>(this);
            processingTask = Task.Run(() => ProcessStartAsync(processContext));
        }
    });

    public Task Send(IMessage message)
    {
        var awaiter = new RequestAwaiter();

        Send(Context.From(builder =>
        {
            message.Configure(builder);
            builder.Set(awaiter);
            builder.Set<IRequestAwaiter>(awaiter);
            builder.Set(message);
        }));

        return awaiter.Completion;
    }

    private void Send(IContext context)
    {
        commands.Schedule(ProcessRequestCommand(context));
    }

    private async Task ProcessStartAsync(IContext context)
    {
        try
        {
            await actor.OnStartAsync(context, cancellation.Token);

            commands.Schedule(ProcessNextRequestCommand);
        }
        catch
        {
            commands.Schedule(RunDisposeCommand);
        }
    }

    private async Task ProcessRequestAsync(IContext context)
    {
        var requestAwaiter = context.Get<RequestAwaiter>();

        try
        {
            await actor.OnReceiveAsync(context.With(processContext), cancellation.Token);

            requestAwaiter?.SetResult();
            commands.Schedule(ProcessNextRequestCommand);
        }
        catch (OperationCanceledException e)
        {
            requestAwaiter?.SetCanceled(e.CancellationToken);
            commands.Schedule(RunDisposeCommand);
        }
        catch (Exception e)
        {
            requestAwaiter?.SetException(e);
            commands.Schedule(RunDisposeCommand);
        }
    }

    private ICommand ProcessRequestCommand(IContext context) => Command.From(() =>
    {
        var requestAwaiter = context.Get<RequestAwaiter>();

        if (cancellation.IsCancellationRequested)
        {
            requestAwaiter?.SetCanceled(cancellation.Token);
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

        if (requests.TryDequeue(out var command))
        {
            command.Execute();
        }
    });

    private ICommand RunDisposeCommand => Command.From(() =>
    {
        if (state >= State.Disposing)
        {
            return;
        }

        using (processCancellation)
        {
            state = State.Disposing;

            cancellation.Cancel();

            while (requests.TryDequeue(out var command))
            {
                command.Execute();
            }

            Task.Run(DisposeInternalAsync);
        }
    });

    public void OnComplete(Action action)
    {
        commands.Schedule(Command.From(() =>
        {
            if (state == State.Disposed)
            {
                action();
            }
            else
            {
                processCompletion = Disposable.Combine(processCompletion, Disposable.Create(action));
            }
        }));
    }

    public void OnCancel(Action action)
    {
        commands.Schedule(Command.From(() =>
        {
            if (state >= State.Disposing)
            {
                action();
            }
            else
            {
                processCancellation = Disposable.Combine(processCancellation, Disposable.Create(action));
            }
        }));
    }

    private ICommand CompleteDisposeCommand => Command.From(() =>
    {
        using (processCompletion)
        {
            state = State.Disposed;
        }
    });

    private async Task DisposeInternalAsync()
    {
        using (completion)
        using (cancellation)
        using (registration)
        await using (actor)
        {
            if (processingTask != null)
            {
                await processingTask;
            }
        }
    }

    private void CompleteDispose()
    {
        commands.Schedule(CompleteDisposeCommand);
    }

    public void Dispose()
    {
        commands.ScheduleAsync(RunDisposeCommand);
    }
}
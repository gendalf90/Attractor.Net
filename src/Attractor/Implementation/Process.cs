using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation;

internal class Process(IActor actor, CancellationToken token) : IAsyncDisposable
{
    private readonly PID pid = PID.Generate();
    private readonly CommandQueue commands = new();
    private readonly Queue<ICommand> requests = new();

    private State state = State.Initial;
    private CancellationTokenRegistration registration;
    private CancellationTokenSource cancellation;
    private Task processingTask;
    private Task disposingTask;
    private IContext processContext;

    public void Start()
    {
        commands.Schedule(ProcessStartCommand);
    }

    private ICommand ProcessStartCommand => Command.From(() =>
    {
        if (state == State.Initial)
        {
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            registration = cancellation.Token.Register(() => commands.Schedule(DisposeCommand));
            state = State.Processing;
            processContext = Context.From(builder =>
            {
                builder.Set(pid);
                builder.Set<ICommandQueueFeature>(commands);
            });
            processingTask = Task.Run(() => ProcessStartAsync(processContext));
        }
    });

    public void Send(IContext context)
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
            commands.Schedule(DisposeCommand);
        }
    }

    private async Task ProcessRequestAsync(IContext context)
    {
        var requestAwaiter = context.Get<RequestAwaiterFeature>();

        try
        {
            using (UseRequestToken(context, out var token))
            {
                await actor.OnReceiveAsync(context.With(processContext), token);

                requestAwaiter?.SetResult();
                commands.Schedule(ProcessNextRequestCommand);
            }
        }
        catch (OperationCanceledException e)
        {
            requestAwaiter?.SetCanceled(e.CancellationToken);
            commands.Schedule(ProcessNextRequestCommand);
        }
        catch (Exception e)
        {
            requestAwaiter?.SetException(e);
            commands.Schedule(DisposeCommand);
        }
    }

    private IDisposable UseRequestToken(IContext context, out CancellationToken token)
    {
        token = cancellation.Token;

        var requestCancellation = context.Get<RequestCancellationFeature>();

        if (requestCancellation == null)
        {
            return Disposable.Empty;
        }

        var requestSource = CancellationTokenSource.CreateLinkedTokenSource(requestCancellation.Token, cancellation.Token);

        token = requestSource.Token;

        return requestSource;
    }

    private ICommand ProcessRequestCommand(IContext context) => Command.From(() =>
    {
        var requestAwaiter = context.Get<RequestAwaiterFeature>();
        var requestCancellation = context.Get<RequestCancellationFeature>();

        if (requestCancellation != null && requestCancellation.Token.IsCancellationRequested)
        {
            requestAwaiter?.SetCanceled(requestCancellation.Token);
        }
        else if (cancellation.IsCancellationRequested)
        {
            requestAwaiter?.SetCanceled(cancellation.Token);
        }
        else if (state == State.Initial)
        {
            requestAwaiter?.SetException(new InvalidOperationException());
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

    private ICommand DisposeCommand => Command.From(() =>
    {
        if (state != State.Disposing)
        {
            state = State.Disposing;
            cancellation?.Cancel();

            while (requests.TryDequeue(out var command))
            {
                command.Execute();
            }

            disposingTask = Task.Run(DisposeInternalAsync);
        }
    });

    private async Task DisposeInternalAsync()
    {
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

    public async ValueTask DisposeAsync()
    {
        await commands.ScheduleAsync(DisposeCommand);
        await disposingTask;
    }
}
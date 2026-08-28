namespace Attractor.Implementation;

public static class Actor
{
    private static readonly AsyncLocal<ICancellation> CurrentCancellation = new();

    private static readonly IHandler Default = new DefaultHandler();

    public static ICancellation Cancellation => CurrentCancellation.Value;

    public static IActor Run(IProps properties, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(properties, nameof(properties));

        var builder = new Builder<IHandler>(Default);

        properties.Configure(builder);

        var handler = builder.Build();
        var process = new Process(handler, token);

        return process;
    }

    private static IDisposable UseCancellation(ICancellation cancellation)
    {
        var current = CurrentCancellation.Value;

        CurrentCancellation.Value = cancellation;

        return Disposable.Create(() => CurrentCancellation.Value = current);
    }

    private class Process : IActor
    {
        private readonly Strand strand;
        private readonly IHandler handler;
        private readonly CancellationToken cancellation;
        private readonly Latch latch;
        private readonly Lock sync;

        private bool disposed = false;

        public Process(IHandler handler, CancellationToken cancellation)
        {
            this.handler = handler;
            this.cancellation = cancellation;
            
            strand = new Strand();
            sync = new Lock();
            latch = new Latch(strand);
        }

        Task IRef.Send(IMessage message, CancellationToken token)
        {
            lock (sync)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(IActor));
                }

                return RunSending(message, token);
            }
        }

        private async Task RunSending(IMessage message, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(message, nameof(message));
            
            using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellation, token);
            
            using (await latch.Use(source.Token))
            using (UseCancellation(this))
            {
                await strand.Run(async () => 
                {
                    await handler.OnReceive(Context.From(message.Configure), source.Token);
                });
            }
        }

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            return new ValueTask(DisposeInternal());
        }

        void IDisposable.Dispose()
        {
            DisposeInternal();
        }

        private Task DisposeInternal()
        {
            lock (sync)
            {
                if (disposed)
                {
                    return Task.CompletedTask;
                }

                disposed = true;

                return RunDisposing();
            }
        }

        private async Task RunDisposing()
        {
            using (await latch.Use())
            {
                await strand.Run(async () => 
                {
                    await handler.DisposeAsync();
                });
            }
        }

        CancellationToken ICancellation.Token => cancellation;
    }

    public static void OnReceive(this IBuilder<IHandler> builder, DecorateReceiveAsync strategy)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        builder.Decorate(() => new HandlerDecorator(onReceive: strategy));
    }

    public static void OnReceive(this IBuilder<IHandler> builder, ReceiveAsync strategy)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        builder.OnReceive(async (next, context, token) =>
        {
            await strategy(context, token);
            await next(context, token);
        });
    }

    public static void OnReceive(this IBuilder<IHandler> builder, Receive strategy)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        builder.OnReceive((context, _) =>
        {
            strategy(context);

            return Task.CompletedTask;
        });
    }

    public static void OnReceive<T>(this IBuilder<IHandler> builder, ReceiveAsync<T> strategy) where T : class
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        builder.OnReceive(async (context, token) =>
        {
            var value = context.Get<T>();

            if (value != null)
            {
                await strategy(value, token);
            }
        });
    }

    public static void OnReceive<T>(this IBuilder<IHandler> builder, Receive<T> strategy) where T : class
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        builder.OnReceive((context) =>
        {
            var value = context.Get<T>();

            if (value != null)
            {
                strategy(value);
            }
        });
    }

    public static void OnDispose(this IBuilder<IHandler> builder, Func<ValueTask> strategy)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        builder.Decorate(() => new HandlerDecorator(disposing: Disposable.Create(strategy)));
    }

    public static void OnDispose(this IBuilder<IHandler> builder, Action strategy)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        builder.Decorate(() => new HandlerDecorator(disposing: Disposable.Async(Disposable.Create(strategy))));
    }

    private class DefaultHandler : IHandler
    {
        Task IHandler.OnReceive(IContext context, CancellationToken token)
        {
            return token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;
        }

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private class HandlerDecorator(DecorateReceiveAsync onReceive = null, IAsyncDisposable disposing = null) : IHandler, IDecorator<IHandler>
    {
        private IHandler decoratee;

        void IDecorator<IHandler>.Decorate(IHandler value)
        {
            decoratee = value;
        }

        Task IHandler.OnReceive(IContext context, CancellationToken token)
        {
            return onReceive == null ? decoratee.OnReceive(context, token) : onReceive(decoratee.OnReceive, context, token);
        }

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            return disposing == null ? decoratee.DisposeAsync() : disposing.With(decoratee).DisposeAsync();
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation;

public static class Actor
{
    public static IActorProcess Run(IProps properties, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(properties, nameof(properties));

        var builder = new ActorBuilder(new DefaultInstance());

        properties.Configure(builder);

        var actor = builder.Build();
        var process = new Process(actor, token);

        process.Start();

        return process;
    }

    public static void Use<T>(this IActorBuilder builder, T value) where T : class
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        Task Strategy(ReceiveAsync next, IContext context, CancellationToken token) => next(context.With(value), token);

        builder.Decorate(() => new DecoratorInstance(onStart: Strategy, onReceive: Strategy));
    }

    public static void OnStart(this IActorBuilder builder, DecorateReceiveAsync strategy)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        builder.Decorate(() => new DecoratorInstance(onStart: strategy));
    }

    public static void OnStart(this IActorBuilder builder, ReceiveAsync strategy)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        builder.OnStart(async (next, context, token) =>
        {
            await next(context, token);
            await strategy(context, token);
        });
    }

    public static void OnStart(this IActorBuilder builder, Receive strategy)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        builder.OnStart((context, _) =>
        {
            strategy(context);

            return Task.CompletedTask;
        });
    }

    public static void OnReceive(this IActorBuilder builder, DecorateReceiveAsync strategy)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        builder.Decorate(() => new DecoratorInstance(onReceive: strategy));
    }

    public static void OnReceive(this IActorBuilder builder, ReceiveAsync strategy)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        builder.OnReceive(async (next, context, token) =>
        {
            await next(context, token);
            await strategy(context, token);
        });
    }

    public static void OnReceive(this IActorBuilder builder, Receive strategy)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        builder.OnReceive((context, _) =>
        {
            strategy(context);

            return Task.CompletedTask;
        });
    }

    public static void OnReceive<T>(this IActorBuilder builder, ReceiveAsync<T> strategy) where T : class
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        builder.OnReceive(async (context, token) =>
        {
            var value = context.Get<T>();

            if (value != null)
            {
                await strategy(value, context, token);
            }
        });
    }

    public static void OnReceive<T>(this IActorBuilder builder, Receive<T> strategy) where T : class
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        builder.OnReceive<T>((value, context, _) =>
        {
            strategy(value, context);

            return Task.CompletedTask;
        });
    }

    public static void OnDispose(this IActorBuilder builder, DecorateDisposeAsync strategy)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        builder.Decorate(() => new DecoratorInstance(onDispose: strategy));
    }

    public static void OnDispose(this IActorBuilder builder, Func<ValueTask> strategy)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        builder.OnDispose(async next =>
        {
            await using (Disposable.Create(strategy))
            {
                await next();
            }
        });
    }

    public static void OnDispose(this IActorBuilder builder, Action strategy)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        ArgumentNullException.ThrowIfNull(strategy, nameof(strategy));

        builder.OnDispose(async next =>
        {
            using (Disposable.Create(strategy))
            {
                await next();
            }
        });
    }

    private class DefaultInstance : IActor
    {
        ValueTask IAsyncDisposable.DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        Task IActor.OnReceiveAsync(IContext context, CancellationToken token)
        {
            return token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;
        }

        Task IActor.OnStartAsync(IContext context, CancellationToken token)
        {
            return token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;
        }
    }

    private class DecoratorInstance(
        DecorateReceiveAsync onReceive = null,
        DecorateReceiveAsync onStart = null,
        DecorateDisposeAsync onDispose = null) : IActor, IDecorator<IActor>
    {
        private IActor decoratee;

        public void Decorate(IActor value)
        {
            decoratee = value;
        }

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            return onDispose == null ? decoratee.DisposeAsync() : onDispose(decoratee.DisposeAsync);
        }

        Task IActor.OnReceiveAsync(IContext context, CancellationToken token)
        {
            return onReceive == null ? decoratee.OnReceiveAsync(context, token) : onReceive(decoratee.OnReceiveAsync, context, token);
        }

        Task IActor.OnStartAsync(IContext context, CancellationToken token)
        {
            return onStart == null ? decoratee.OnStartAsync(context, token) : onStart(decoratee.OnStartAsync, context, token);
        }
    }
}

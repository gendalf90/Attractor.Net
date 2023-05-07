using System;
using System.Threading;
using Attractor.Implementation;

namespace Attractor
{
    public static class BuilderExtensions
    {
        public static void MessageProcessingTimeout(this IActorBuilder builder, TimeSpan timeout)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));

            builder.DecorateActor(() =>
            {
                return Actor.FromStrategy(async (next, context, token) =>
                {
                    using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);

                    cancellation.CancelAfter(timeout);

                    await next(context, cancellation.Token);
                });
            });
        }

        public static void Chain<T>(this IActorBuilder builder, Func<T> factory) where T : class, IActor
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));
            ArgumentNullException.ThrowIfNull(factory, nameof(factory));

            builder.DecorateActor(() => ChainActor(factory()));
        }

        public static void Chain<T>(this IServicesActorBuilder builder, Func<IServiceProvider, T> factory) where T : class, IActor
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));
            ArgumentNullException.ThrowIfNull(factory, nameof(factory));

            builder.DecorateActor(provider => ChainActor(factory(provider)));
        }

        private static IActorDecorator ChainActor(IActor actor)
        {
            return Actor.FromStrategy(
                async (next, context, token) => 
                {
                    await actor.OnReceiveAsync(context, token);
                    await next(context, token);
                },
                async (next, context, error, token) =>
                {
                    await actor.OnErrorAsync(context, error, token);
                    await next(context, error, token);
                },
                async (next) =>
                {
                    await actor.DisposeAsync();
                    await next();
                });
        }
    }
}
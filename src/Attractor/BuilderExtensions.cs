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

        public static void MessageReceivingTimeout(this IActorBuilder builder, TimeSpan timeout)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));

            DynamicType.Invoke(new MessageReceivingTimeoutRegistrator(builder, timeout));
        }

        private record MessageReceivingTimeoutRegistrator(IActorBuilder Builder, TimeSpan Timeout) : IDynamicExecutor
        {
            void IDynamicExecutor.Invoke<T>()
            {
                Builder.DecorateMailbox(() =>
                {
                    var timeoutData = new ContextData<T> { Timeout = Timeout };

                    return Mailbox.FromStrategy(async (next, token) =>
                    {
                        timeoutData.Source?.Token.ThrowIfCancellationRequested();
                        
                        var result = await next(token);

                        timeoutData.Source?.Dispose();
                        timeoutData.Source = null;

                        result.Set(timeoutData);

                        return result;
                    });
                });

                Builder.DecorateActor(() =>
                {
                    ContextData<T> timeoutData = null;
                    
                    return Actor.FromStrategy(
                        async (next, context, token) =>
                        {
                            timeoutData = context.Get<ContextData<T>>();

                            var process = context.Get<IActorProcess>();

                            timeoutData.Source = new CancellationTokenSource(timeoutData.Timeout);

                            timeoutData.Source.Token.Register(() => process?.Awake());

                            await next(context, token);
                        },
                        async (next) =>
                        {
                            timeoutData.Source?.Dispose();
                            timeoutData.Source = null;
                            
                            await next();
                        });
                });
            }
        }

        public static void ActorInstanceTTL(this IActorBuilder builder, TimeSpan ttl)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));

            DynamicType.Invoke(new ActorInstanceTTLRegistrator(builder, ttl));
        }

        private record ActorInstanceTTLRegistrator(IActorBuilder Builder, TimeSpan Timeout) : IDynamicExecutor
        {
            void IDynamicExecutor.Invoke<T>()
            {
                Builder.DecorateMailbox(() =>
                {
                    var timeoutData = new ContextData<T> { Timeout = Timeout };

                    return Mailbox.FromStrategy(async (next, token) =>
                    {
                        timeoutData.Source?.Token.ThrowIfCancellationRequested();
                    
                        var result = await next(token);

                        result.Set(timeoutData);

                        return result;
                    });
                });

                Builder.DecorateActor(() =>
                {
                    ContextData<T> timeoutData = null;
                    
                    return Actor.FromStrategy(
                        async (next, context, token) =>
                        {
                            if (timeoutData == null)
                            {
                                timeoutData = context.Get<ContextData<T>>();

                                var process = context.Get<IActorProcess>();

                                timeoutData.Source = new CancellationTokenSource(timeoutData.Timeout);

                                timeoutData.Source.Token.Register(() => process?.Awake());
                            }

                            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutData.Source.Token);

                            await next(context, cancellation.Token);
                        },
                        async (next) =>
                        {
                            timeoutData.Source?.Dispose();
                            timeoutData.Source = null;
                            
                            await next();
                        });
                });
            }
        }

        public static void MessageProcessingLimit(this IActorBuilder builder, long limit)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));

            DynamicType.Invoke(new MessageProcessingLimitRegistrator(builder, limit));
        }

        private record MessageProcessingLimitRegistrator(IActorBuilder Builder, long Limit) : IDynamicExecutor
        {
            void IDynamicExecutor.Invoke<T>()
            {
                Builder.DecorateMailbox(() =>
                {
                    var limitData = new ContextData<T>();

                    return Mailbox.FromStrategy(async (next, token) =>
                    {
                        if (limitData.Counter == Limit)
                        {
                            throw new OperationCanceledException();
                        }
                    
                        var result = await next(token);

                        result.Set(limitData);

                        return result;
                    });
                });

                Builder.DecorateActor(() =>
                {
                    ContextData<T> limitData = null;
                    
                    return Actor.FromStrategy(
                        async (next, context, token) =>
                        {
                            limitData ??= new ContextData<T>();

                            if (++limitData.Counter == Limit)
                            {
                                context.Get<IActorProcess>()?.Awake();
                            }

                            await next(context, token);
                        },
                        async (next) =>
                        {
                            if (limitData != null)
                            {
                                limitData.Counter = 0;
                            }
                            
                            await next();
                        });
                });
            }
        }

        public static void UseMessageAutoConsume(this IActorBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));

            builder.DecorateActor(() =>
            {
                return Actor.FromStrategy(
                    async (next, context, token) =>
                    {
                        await next(context, token);

                        var consumable = context.Get<IConsumable>();
                                
                        if (consumable != null)
                        {
                            await consumable.ConsumeAsync(token);
                        }
                    });
            });
        }
        
        public static void ChainActor<T>(this IActorBuilder builder, Func<T> factory) where T : class, IActor
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));
            ArgumentNullException.ThrowIfNull(factory, nameof(factory));

            builder.DecorateActor(() => ChainActor(factory()));
        }

        public static void ChainActor<T>(this IServicesActorBuilder builder, Func<IServiceProvider, T> factory) where T : class, IActor
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
                async (next) =>
                {
                    await actor.DisposeAsync();
                    await next();
                });
        }

        private class ContextData<TKey>
        {
            public TimeSpan Timeout { get; set; }

            public CancellationTokenSource Source { get; set; }

            public long Counter { get; set; }
        }
    }
}
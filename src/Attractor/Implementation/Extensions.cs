using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Attractor.Implementation
{
    public static class Extensions
    {
        public static IActorRef WithRefresh(this IActorRef actorRef)
        {
            return new RefreshDecorator(actorRef);
        }

        private class RefreshDecorator : IActorRef
        {
            private State state;

            public RefreshDecorator(IActorRef decoratee)
            {
                state = new State(this, decoratee);
            }

            ValueTask IActorRef.SendAsync(IContext context, CancellationToken token)
            {
                return Volatile.Read(ref state).SendAsync(context, token);
            }

            private class State : IActorDecorator
            {
                private const int Processing = 0;
                private const int Stopped = 1;

                private readonly RefreshDecorator parent;
                private readonly IActorRef actorRef;

                private IActor next;
                private int state = Processing;

                public State(RefreshDecorator parent, IActorRef actorRef)
                {
                    this.parent = parent;
                    this.actorRef = actorRef;
                }

                public ValueTask SendAsync(IContext context, CancellationToken token)
                {
                    context[this] = this;

                    return actorRef.SendAsync(context, token);
                }

                ValueTask IActor.OnReceiveAsync(IContext context, CancellationToken token)
                {
                    var process = context.Get<IActorProcess>();
                    var system = context.Get<IActorSystem>();
                    var address = context.Get<IAddress>();

                    if (process.IsCollecting() && Interlocked.CompareExchange(ref state, Stopped, Processing) == Processing)
                    {
                        Volatile.Write(ref parent.state, new State(parent, system.Refer(address)));
                    }

                    return next.OnReceiveAsync(context, token);
                }

                void IDecorator<IActor>.Decorate(IActor value)
                {
                    next = value;
                }
            }
        }

        public static void WithReceivingTimeout(this IActorBuilder builder, TimeSpan timeout, IMessageFilter filter = null)
        {
            builder.Decorate(() => new ReceivingTimeoutDecorator(timeout, filter));
        }

        private class ReceivingTimeoutDecorator : IActorDecorator
        {
            private readonly Stopwatch stopwatch = new();
            private readonly object sync = new();

            private readonly TimeSpan timeout;
            private readonly IMessageFilter filter;

            private Timer timer;
            private IActor decoratee;
            private bool needStopAndRestart;

            public ReceivingTimeoutDecorator(TimeSpan timeout, IMessageFilter filter)
            {
                this.timeout = timeout;
                this.filter = filter;
            }

            void IDecorator<IActor>.Decorate(IActor value)
            {
                decoratee = value;
            }

            private async ValueTask BeforeProcessingAsync(IContext context, CancellationToken token)
            {
                needStopAndRestart = filter == null || await filter.IsMatchAsync(context, token);

                if (needStopAndRestart)
                {
                    lock (sync)
                    {
                        stopwatch.Stop();
                    }
                }
            }

            async ValueTask IActor.OnReceiveAsync(IContext context, CancellationToken token)
            {
                try
                {
                    await BeforeProcessingAsync(context, token);

                    await decoratee.OnReceiveAsync(context, token);
                }
                finally
                {
                    AfterProcessing(context);
                }
            }

            private void AfterProcessing(IContext context)
            {
                var process = context.Get<IActorProcess>();

                if (process == null)
                {
                    return;
                }

                lock (sync)
                {
                    stopwatch.Stop();

                    if (TryStop(process))
                    {
                        return;
                    }

                    TryStartTimer(process, timeout);

                    if (needStopAndRestart)
                    {
                        stopwatch.Restart();
                    }
                    else
                    {
                        stopwatch.Start();
                    }
                }
            }

            private void ClearTimer()
            {
                timer?.Dispose();
                timer = null;
            }

            private bool TryStop(IActorProcess process)
            {
                if (!process.IsActive())
                {
                    return true;
                }

                if (stopwatch.Elapsed < timeout)
                {
                    return false;
                }

                process.Stop();

                return true;
            }

            private void TryStartTimer(IActorProcess process, TimeSpan dueTime)
            {
                if (timer != null)
                {
                    return;
                }

                timer = new Timer(
                    Callback,
                    process,
                    dueTime,
                    Timeout.InfiniteTimeSpan);
            }

            private void Callback(object obj)
            {
                var process = (IActorProcess)obj;

                lock (sync)
                {
                    stopwatch.Stop();

                    ClearTimer();

                    if (TryStop(process))
                    {
                        return;
                    }

                    TryStartTimer(process, timeout - stopwatch.Elapsed);

                    stopwatch.Start();
                }
            }
        }

        public static void Set<T>(this IContext context, T value) where T : class
        {
            if (value == null)
            {
                context.Remove(typeof(T));
            }
            else
            {
                context[typeof(T)] = value;
            }
        }

        public static T Get<T>(this IContext context) where T : class
        {
            if (context.TryGetValue(typeof(T), out var result) && result is T typed)
            {
                return typed;
            }

            return null;
        }

        public static T Aggregate<T>(this IContext context, T accumulator, Func<T, KeyValuePair<object, object>, T> action)
        {
            var result = accumulator;
            
            if (context is Dictionary<object, object> dictionary)
            {
                foreach (var pair in dictionary)
                {
                    result = action(result, pair);
                }
            }
            else
            {
                foreach (var pair in context)
                {
                    result = action(result, pair);
                }
            }

            return result;
        }

        public static void Chain(this IActorBuilder builder, Action<IActorBuilder> configuration)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

            var current = new ActorBuilder();

            configuration(current);

            builder.Decorate(() =>
            {
                var result = current.Build();

                return Actor.FromStrategy(async (next, context, token) =>
                {
                    await next(context, token);
                    await result.OnReceiveAsync(context, token);
                });
            });
        }

        public static void OnStart(this IActorBuilder builder, Action<IActorBuilder> configuration)
        {
            ChainIf(builder, configuration, process => process.IsStarting());
        }

        public static void OnStop(this IActorBuilder builder, Action<IActorBuilder> configuration)
        {
            ChainIf(builder, configuration, process => process.IsStopping());
        }

        public static void OnActive(this IActorBuilder builder, Action<IActorBuilder> configuration)
        {
            ChainIf(builder, configuration, process => process.IsActive());
        }

        public static void OnCollect(this IActorBuilder builder, Action<IActorBuilder> configuration)
        {
            ChainIf(builder, configuration, process => process.IsCollecting());
        }

        private static void ChainIf(IActorBuilder builder, Action<IActorBuilder> configuration, Predicate<IActorProcess> condition)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));
            ArgumentNullException.ThrowIfNull(condition, nameof(condition));

            builder.Chain(current =>
            {
                configuration(current);

                current.Decorate(() => Actor.FromStrategy(async (next, context, token) =>
                {
                    var process = context.Get<IActorProcess>();

                    if (process == null)
                    {
                        return;
                    }
                    
                    if (condition(context.Get<IActorProcess>()))
                    {
                        await next(context, token);
                    }
                }));
            });
        }

        public static Action<IActorBuilder> Register<T>(this T actor) where T : class, IActor
        {
            return builder =>
            {
                builder.Register(() => actor);
            };
        }

        public static IServiceCollection AddActorSystem(
            this IServiceCollection services, 
            Action<IServiceProvider, IActorBuilder> configuration = null,
            CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(services, nameof(services));

            return services.AddSingleton(provider =>
            {
                var system = ActorSystem.Create(token);

                foreach (var builder in provider.GetServices<ServicesActorBuilder>())
                {
                    system.Register(builder.AddressPolicy, Partial(configuration, provider) + Partial(builder.Configuration, provider));
                }

                return system;
            });
        }

        public static IServiceCollection AddActor(
            this IServiceCollection services, 
            IAddressPolicy policy, 
            Action<IServiceProvider, IActorBuilder> configuration = null)
        {
            ArgumentNullException.ThrowIfNull(services, nameof(services));
            ArgumentNullException.ThrowIfNull(policy, nameof(policy));

            return services.AddSingleton(new ServicesActorBuilder(policy, configuration));
        }

        private static Action<TSecond> Partial<TFirst, TSecond>(Action<TFirst, TSecond> func, TFirst value)
        {
            return input => func?.Invoke(value, input);
        }

        private record ServicesActorBuilder(IAddressPolicy AddressPolicy, Action<IServiceProvider, IActorBuilder> Configuration);
    }
}
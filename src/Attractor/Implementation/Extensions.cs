using System;
using System.Collections;
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

            ValueTask IActorRef.SendAsync(IList context, CancellationToken token)
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

                public ValueTask SendAsync(IList context, CancellationToken token)
                {
                    if (!context.Contains(this))
                    {
                        context.Add(this);
                    }
                    
                    return actorRef.SendAsync(context, token);
                }

                ValueTask IActor.OnReceiveAsync(IList context, CancellationToken token)
                {
                    var (process, system, address) = context.Find<IActorProcess, IActorSystem, IAddress>();

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

            private async ValueTask BeforeProcessingAsync(IList context, CancellationToken token)
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

            async ValueTask IActor.OnReceiveAsync(IList context, CancellationToken token)
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

            private void AfterProcessing(IList context)
            {
                var process = context.Find<IActorProcess>();

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

        public static void Set<T>(this IList context, T value) where T : class
        {
            context.Set(value, (NonSettable)null, (NonSettable)null, (NonSettable)null);
        }

        public static void Set<T1, T2>(
            this IList context, 
            T1 first,
            T2 second) 
            where T1 : class
            where T2 : class
        {
            context.Set(first, second, (NonSettable)null, (NonSettable)null);
        }

        public static void Set<T1, T2, T3>(
            this IList context, 
            T1 first,
            T2 second,
            T3 third) 
            where T1 : class
            where T2 : class
            where T3 : class
        {
            context.Set(first, second, third, (NonSettable)null);
        }

        public static void Set<T1, T2, T3, T4>(
            this IList context, 
            T1 first,
            T2 second,
            T3 third,
            T4 fourth) 
            where T1 : class
            where T2 : class
            where T3 : class
            where T4 : class
        {
            var isFirstSet = first is T2 or T3 or T4 or NonSettable;
            var isSecondSet = second is T3 or T4 or NonSettable;
            var isThirdSet = third is T4 or NonSettable;
            var isFourthSet = fourth is NonSettable;
            
            for (int i = 0; i < context.Count; i++)
            {
                ProcessSet(context, i, first, ref isFirstSet);
                ProcessSet(context, i, second, ref isSecondSet);
                ProcessSet(context, i, third, ref isThirdSet);
                ProcessSet(context, i, fourth, ref isFourthSet);
            }

            ProcessAdd(context, first, isFirstSet);
            ProcessAdd(context, second, isSecondSet);
            ProcessAdd(context, third, isThirdSet);
            ProcessAdd(context, fourth, isFourthSet);
        }

        private static void ProcessSet<T>(IList context, int index, T value, ref bool isSet)
        {
            if (context[index] is not T)
            {
                return;
            }

            if (isSet)
            {
                context[index] = null;
            }
            else
            {
                context[index] = value;

                isSet = true;
            }
        }

        private static void ProcessAdd<T>(IList context, T value, bool isSet)
        {
            if (!isSet)
            {
                context.Add(value);
            }
        }

        private record NonSettable();

        public static (T1 first, T2 second, T3 third, T4 fourth) Find<T1, T2, T3, T4>(this IList context) 
            where T1 : class
            where T2 : class
            where T3 : class
            where T4 : class
        {
            (T1 first, T2 second, T3 third, T4 fourth) result = default;
            
            foreach (var item in context)
            {
                if (result.first is null && item is T1 first)
                {
                    result.first = first;
                }

                if (result.second is null && item is T2 second)
                {
                    result.second = second;
                }

                if (result.third is null && item is T3 third)
                {
                    result.third = third;
                }

                if (result.fourth is null && item is T4 fourth)
                {
                    result.fourth = fourth;
                }
            }

            return result;
        }

        public static (T1 first, T2 second, T3 third) Find<T1, T2, T3>(this IList context) 
            where T1 : class
            where T2 : class
            where T3 : class
        {
            var result = Find<T1, T2, T3, object>(context);

            return (result.first, result.second, result.third);
        }

        public static (T1 first, T2 second) Find<T1, T2>(this IList context) 
            where T1 : class
            where T2 : class
        {
            var result = Find<T1, T2, object, object>(context);

            return (result.first, result.second);
        }

        public static T Find<T>(this IList context) where T : class
        {
            var result = Find<T, object, object, object>(context);

            return result.first;
        }

        public static void Chain(this IActorBuilder builder, IActor actor)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));
            ArgumentNullException.ThrowIfNull(actor, nameof(actor));

            builder.Decorate(() => Actor.FromStrategy(async (next, context, token) =>
            {
                await next(context, token);
                await actor.OnReceiveAsync(context, token);
            }));
        }

        public static void Start(this IActorBuilder builder, IActor actor)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));
            ArgumentNullException.ThrowIfNull(actor, nameof(actor));

            builder.Chain(new ProcessActor(OnStart: actor.OnReceiveAsync));
        }

        public static void Active(this IActorBuilder builder, IActor actor)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));
            ArgumentNullException.ThrowIfNull(actor, nameof(actor));

            builder.Chain(new ProcessActor(OnActive: actor.OnReceiveAsync));
        }

        public static void Stop(this IActorBuilder builder, IActor actor)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));
            ArgumentNullException.ThrowIfNull(actor, nameof(actor));

            builder.Chain(new ProcessActor(OnStop: actor.OnReceiveAsync));
        }

        public static void Collect(this IActorBuilder builder, IActor actor)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));
            ArgumentNullException.ThrowIfNull(actor, nameof(actor));

            builder.Chain(new ProcessActor(OnCollect: actor.OnReceiveAsync));
        }

        private record ProcessActor(
            OnReceive OnStart = null,
            OnReceive OnStop = null,
            OnReceive OnActive = null,
            OnReceive OnCollect = null
        ) : IActor
        {
            async ValueTask IActor.OnReceiveAsync(IList context, CancellationToken token)
            {
                var process = context.Find<IActorProcess>();

                if (process == null)
                {
                    return;
                }

                if (process.IsStarting())
                {
                    await CallAsync(OnStart, context, token);
                    await CallAsync(OnActive, context, token);
                }
                else if (process.IsStopping())
                {
                    await CallAsync(OnStop, context, token);
                    await CallAsync(OnActive, context, token);
                }
                else if (process.IsActive())
                {
                    await CallAsync(OnActive, context, token);
                }
                else if (process.IsCollecting())
                {
                    await CallAsync(OnCollect, context, token);
                }
            }

            ValueTask CallAsync(OnReceive onReceive, IList context, CancellationToken token)
            {
                return onReceive != null ? onReceive(context, token) : default;
            }
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
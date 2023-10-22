using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
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

        public static async ValueTask ProcessAsync(this IActorRef actorRef, IList context, CancellationToken token = default)
        {
            var decorator = new ProcessingDecorator();
            var registration = token.Register(Cancellation, decorator);

            try
            {
                if (!context.Contains(decorator))
                {
                    context.Add(decorator);
                }

                await actorRef.SendAsync(context, token);

                await decorator.Task;
            }
            finally
            {
                registration.Dispose();
            }
        }

        private static Action<object, CancellationToken> Cancellation = (state, token) =>
        {
            (state as TaskCompletionSource)?.TrySetCanceled(token);
        };

        private class ProcessingDecorator : TaskCompletionSource, IActorDecorator
        {
            private IActor decoratee;
            
            void IDecorator<IActor>.Decorate(IActor value)
            {
                decoratee = value;
            }

            async ValueTask IActor.OnReceiveAsync(IList context, CancellationToken token)
            {
                var process = context.Find<IActorProcess>();

                if (!process.IsProcessing())
                {
                    await decoratee.OnReceiveAsync(context, token);
                }
                else
                {
                    await ProcessAsync(context, token);
                }
            }

            private async ValueTask ProcessAsync(IList context, CancellationToken token)
            {
                try
                {
                    await decoratee.OnReceiveAsync(context, token);

                    TrySetResult();
                }
                catch (Exception e)
                {
                    TrySetException(e);
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

            private readonly TimeSpan timeout;
            private readonly IMessageFilter filter;

            private Timer timer;
            private IActor decoratee;
            private RestartTimerMessage restart;
            private IActorProcess process;
            private PID pid;
            private bool needStopAndRestartWatch;
            
            public ReceivingTimeoutDecorator(TimeSpan timeout, IMessageFilter filter)
            {
                this.timeout = timeout;
                this.filter = filter;
            }

            void IDecorator<IActor>.Decorate(IActor value)
            {
                decoratee = value;
            }

            private void Init(IList context)
            {
                (restart, process, pid) = context.Find<RestartTimerMessage, IActorProcess, PID>();
            }

            private void BeforeProcessing(IList context)
            {
                if (restart != null || process.IsStarting())
                {
                    return;
                }

                needStopAndRestartWatch = filter == null || filter.IsMatch(context);
                
                if (needStopAndRestartWatch)
                {
                    stopwatch.Stop();
                }
            }

            async ValueTask IActor.OnReceiveAsync(IList context, CancellationToken token)
            {
                try
                {
                    Init(context);
                    BeforeProcessing(context);
                    
                    await decoratee.OnReceiveAsync(context, token);
                }
                finally
                {
                    AfterProcessing();
                }
            }

            private void AfterProcessing()
            {
                StopWatch();
                
                if (TryStart())
                {
                    return;
                }
                
                if (TryStop())
                {
                    return;
                }

                RestartTimerIfNeeded();
                RestartOrResumeWatch();
            }

            private void StopWatch()
            {
                stopwatch.Stop();
            }

            private void RestartOrResumeWatch()
            {
                if (needStopAndRestartWatch)
                {
                    stopwatch.Restart();
                }
                else
                {
                    stopwatch.Start();
                }
            }

            private bool TryStart()
            {
                if (!process.IsStarting())
                {
                    return false;
                }

                StartTimer(timeout);

                stopwatch.Start();

                return true;
            }

            private bool TryStop()
            {
                if (!process.IsProcessing())
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

            private bool RestartTimerIfNeeded()
            {
                if (restart == null || restart.PID != pid)
                {
                    return false;
                }

                StartTimer(timeout - stopwatch.Elapsed);

                return true;
            }

            private void StartTimer(TimeSpan dueTime)
            {
                timer?.Dispose();
                
                timer = new Timer(
                    Callback, 
                    new RestartTimerMessage(process, pid), 
                    dueTime, 
                    Timeout.InfiniteTimeSpan);
            }

            private void Callback(object obj)
            {
                var message = (RestartTimerMessage)obj;
                var context = Context.System();

                context.Set(message);

                _ = message.ActorRef.SendAsync(context);
            }

            private record RestartTimerMessage(IActorRef ActorRef, PID PID);
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

        public static IServiceCollection AddActorSystem(
            this IServiceCollection services, 
            Action<IServiceProvider, IActorBuilder> configuration = null,
            CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(services, nameof(services));

            return services.AddSingleton(provider =>
            {
                var system = ActorSystem.Create(token);

                foreach (var builder in provider.GetServices<ActorBuilder>())
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

            return services.AddSingleton(new ActorBuilder(policy, configuration));
        }

        private static Action<TSecond> Partial<TFirst, TSecond>(Action<TFirst, TSecond> func, TFirst value)
        {
            return input => func?.Invoke(value, input);
        }

        private record ActorBuilder(IAddressPolicy AddressPolicy, Action<IServiceProvider, IActorBuilder> Configuration);
    }
}
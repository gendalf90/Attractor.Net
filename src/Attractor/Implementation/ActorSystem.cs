using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Attractor.Implementation
{
    public sealed class ActorSystem : IActorSystem
    {
        private readonly Dictionary<IAddress, ActorProcess> processes = new(AddressEqualityComparer.Default);
        private readonly LinkedList<ActorProcessBuilder> builders = new();
        private readonly CommandQueue<ICommand> commands = new();

        private readonly CancellationToken token;

        private ActorSystem(CancellationToken token)
        {
            this.token = token;
        }
        
        public static IActorSystem Create(CancellationToken token = default)
        {
            return new ActorSystem(token);
        }

        IActorRef IActorSystem.Refer(IAddress address)
        {
            token.ThrowIfCancellationRequested();

            return new ActorRef(this, address);
        }

        void IActorSystem.Register(IAddressPolicy policy, Action<IActorBuilder> configuration)
        {
            token.ThrowIfCancellationRequested();

            var builder = new ActorProcessBuilder(this, policy);

            configuration?.Invoke(builder);

            commands.Schedule(new StrategyCommand(() =>
            {
                builders.AddFirst(builder);
            }));
        }

        private Task<ActorProcess> GetOrCreateProcessAsync(IAddress address)
        {
            var completion = new TaskCompletionSource<ActorProcess>();

            commands.Schedule(new StrategyCommand(() =>
            {
                if (processes.TryGetValue(address, out var result))
                {
                    completion.SetResult(result);
                }
                else
                {
                    commands.Schedule(new TryBuildProcessCommand(completion, builders.First, address, this));
                }
            }));

            return completion.Task;
        }

        private record TryBuildProcessCommand(
            TaskCompletionSource<ActorProcess> Completion,
            LinkedListNode<ActorProcessBuilder> Node,
            IAddress Address, 
            ActorSystem System) : ICommand
        {
            ValueTask ICommand.ExecuteAsync()
            {
                try
                {
                    if (System.processes.TryGetValue(Address, out var result))
                    {
                        Completion.SetResult(result);
                    }
                    else if (Node == null)
                    {
                        Completion.SetException(new InvalidOperationException());
                    }
                    else if (Node.Value.TryBuildProcess(Address, out var process))
                    {
                        System.processes.Add(Address, process);

                        Completion.SetResult(process);
                    }
                    else
                    {
                        System.commands.Schedule(new TryBuildProcessCommand(Completion, Node.Next, Address, System));
                    }
                }
                catch (Exception ex)
                {
                    Completion.SetException(ex);
                }
                
                return ValueTask.CompletedTask;
            }
        }

        private void RemoveProcess(IAddress address)
        {
            commands.Schedule(new StrategyCommand(() =>
            {
                processes.Remove(address);
            }));
        }

        private class ActorRef : IActorRef
        {
            private readonly object sync = new();
            
            private readonly ActorSystem system;
            private readonly IAddress address;
            
            private IActorRef state;

            public ActorRef(ActorSystem system, IAddress address)
            {
                this.system = system;
                this.address = address;
            }
            
            ValueTask IActorRef.PostAsync(IContext context, CancellationToken token)
            {
                var current = state;

                if (current == null)
                {
                    lock (sync)
                    {
                        current = state ??= new ActorRefState(this, system.GetOrCreateProcessAsync(address));
                    }
                }
                
                return current.PostAsync(context, token);
            }

            public void ClearState()
            {
                lock (sync)
                {
                    state = null;
                }
            }
        }

        private class ActorRefState : IActorRef
        {
            private readonly ActorRef parent;
            private readonly Task<ActorProcess> promise;

            private bool stopped;

            public ActorRefState(ActorRef parent, Task<ActorProcess> promise)
            {
                this.parent = parent;
                this.promise = promise;
            }

            async ValueTask IActorRef.PostAsync(IContext context, CancellationToken token)
            {
                IActorRef process = await promise.WaitAsync(token);

                context.Set(this);

                await process.PostAsync(context, token);
            }

            public void Stop()
            {
                if (stopped)
                {
                    return;
                }

                stopped = true;

                parent.ClearState();
            }
        }

        private class ActorProcess : IActorProcess
        {
            private const int Processing = 0;
            private const int Stopping = 1;
            private const int Stopped = 2;

            private readonly PID pid = PID.Generate();
            private readonly CommandQueue<ProcessMessageCommand> commands = new();

            private readonly ActorSystem system;
            private readonly IAddress address;
            private readonly CancellationTokenSource cancellation;

            private readonly Func<IContext, CancellationToken, ValueTask> receive;
            private readonly Func<ValueTask> dispose;
            private readonly Func<IContext, ValueTask> push;

            private ActorProcess next;
            private int state;

            public ActorProcess(ActorSystem system, IActor actor, IAddress address)
            {
                this.system = system;
                this.address = address;

                receive = actor.OnReceiveAsync;
                dispose = actor.DisposeAsync;
                push = PushAsync;

                cancellation = CancellationTokenSource.CreateLinkedTokenSource(system.token);
            }

            ValueTask IActorRef.PostAsync(IContext context, CancellationToken token)
            {
                ArgumentNullException.ThrowIfNull(context, nameof(context));

                token.ThrowIfCancellationRequested();
                system.token.ThrowIfCancellationRequested();

                Post(context);

                return default;
            }

            private void Post(IContext context)
            {
                commands.Schedule(new ProcessMessageCommand
                {
                    Process = this,
                    Context = context
                });
            }

            private readonly struct ProcessMessageCommand : ICommand
            {
                public ActorProcess Process { get; init; }
                
                public IContext Context { get; init; }
                
                ValueTask ICommand.ExecuteAsync()
                {
                    return Process.ReceiveAsync(Context);
                }
            }

            private async ValueTask ReceiveAsync(IContext context)
            {
                SetContext(context);
                OnStopped(context);

                if (await TryPushAsync(context))
                {
                    return;
                }

                try
                {
                    await OnReceiveAsync(context);
                }
                finally
                {
                    await OnClearAsync(context);
                }
            }

            private async ValueTask OnClearAsync(IContext context)
            {
                if (Interlocked.CompareExchange(ref state, Stopped, Stopping) != Stopping)
                {
                    return;
                }

                using (cancellation)
                using (Disposable.Create(() => context.Get<ActorRefState>()?.Stop()))
                using (Disposable.Create(() => system.RemoveProcess(address)))
                {
                    await OnDisposeAsync(context);
                }
            }

            private void OnStopped(IContext context)
            {
                if (state == Stopped)
                {
                    context.Get<ActorRefState>()?.Stop();
                }
            }

            private async ValueTask<bool> TryPushAsync(IContext context)
            {
                if (state != Stopped)
                {
                    return false;
                }

                await OnPushAsync(context);

                return true;
            }

            private ValueTask OnPushAsync(IContext context)
            {
                var decorator = context.Get<OnPushDecorator>();

                return decorator == null ? push(context) : decorator(push, context);
            }

            private async ValueTask PushAsync(IContext context)
            {
                next ??= await system.GetOrCreateProcessAsync(address);

                next.Post(context);
            }

            private ValueTask OnReceiveAsync(IContext context)
            {
                var decorator = context.Get<OnReceiveDecorator>();

                return decorator == null ? receive(context, cancellation.Token) : decorator(receive, context, cancellation.Token);
            }

            private ValueTask OnDisposeAsync(IContext context)
            {
                var decorator = context.Get<OnDisposeDecorator>();

                return decorator == null ? dispose() : decorator(dispose);
            }

            private void SetContext(IContext context)
            {
                context.Set(pid);
                context.Set(address);
                context.Set<IActorSystem>(system);
                context.Set<IActorProcess>(this);
            }

            void IActorProcess.Stop()
            {
                if (Interlocked.CompareExchange(ref state, Stopping, Processing) != Processing)
                {
                    return;
                }

                cancellation.Cancel();

                var context = Context.Default();

                context.Set<OnPushDecorator>((_, _) => default);

                Post(context);
            }
        }

        private class ActorProcessBuilder : IActorBuilder
        {
            private readonly ActorSystem system;
            private readonly IAddressPolicy policy;

            private Func<IActor, IActor> actorDecoratorFactory = _ => _;
            private Func<IActor> defaultActorFactory = Actor.Empty;

            public ActorProcessBuilder(ActorSystem system, IAddressPolicy policy)
            {
                this.system = system;
                this.policy = policy;
            }

            void IActorBuilder.RegisterActor<T>(Func<T> factory)
            {
                defaultActorFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            }

            void IActorBuilder.DecorateActor<T>(Func<T> factory)
            {
                ArgumentNullException.ThrowIfNull(factory, nameof(factory));

                actorDecoratorFactory = Decorate(actorDecoratorFactory, factory);
            }

            public bool TryBuildProcess(IAddress address, out ActorProcess process)
            {
                process = null;

                if (!policy.IsMatch(address))
                {
                    return false;
                }

                process = new ActorProcess(system, actorDecoratorFactory(defaultActorFactory()), address);

                return true;
            }

            private static Func<TResult, TResult> Decorate<TResult, TDecorator>(Func<TResult, TResult> resultFactory, Func<TDecorator> decoratorFactory) 
                where TDecorator : class, TResult, IDecorator<TResult>
            {
                return result =>
                {
                    var decorator = decoratorFactory();

                    decorator.Decorate(result);

                    return resultFactory(decorator);
                };
            }
        }
    }
}
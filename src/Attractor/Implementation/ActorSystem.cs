using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Attractor.Implementation
{
    public sealed class ActorSystem : IActorSystem
    {
        static ActorSystem()
        {
            Context.Cache<IAddress>();
            Context.Cache<IPayload>();
            Context.Cache<IActorSystem>();
            Context.Cache<ISupervisor>();
        }
        
        private readonly Dictionary<IAddress, ActorProcess> processes = new(AddressEqualityComparer.Default);
        private readonly LinkedList<ActorProcessBuilder> builders = new();
        private readonly CommandQueue commands = new();

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
            return new ActorRef(this, address);
        }

        void IActorSystem.Register(IAddressPolicy policy, Action<IActorBuilder> configuration)
        {
            var builder = new ActorProcessBuilder(this, policy);

            configuration?.Invoke(builder);

            commands.Schedule(new StrategyCommand(() =>
            {
                builders.AddLast(builder);
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
            
            ValueTask IActorRef.PostAsync(IPayload payload, Action<IContext> configuration, CancellationToken token)
            {
                var current = state;

                if (current == null)
                {
                    lock (sync)
                    {
                        current = state ??= new State(this, system.GetOrCreateProcessAsync(address));
                    }
                }
                
                return current.PostAsync(payload, configuration, token);
            }

            private class State : IActorRef
            {
                private readonly ActorRef parent;
                private readonly Task<ActorProcess> promise;

                private bool stopped;

                public State(ActorRef parent, Task<ActorProcess> promise)
                {
                    this.parent = parent;
                    this.promise = promise;
                }

                async ValueTask IActorRef.PostAsync(IPayload payload, Action<IContext> configuration, CancellationToken token)
                {
                    ArgumentNullException.ThrowIfNull(payload, nameof(payload));
                
                    parent.system.token.ThrowIfCancellationRequested();

                    var context = Context.Default();

                    context.Set(parent.address);
                    context.Set(payload);
                    context.Set<IActorSystem>(parent.system);
                    
                    configuration?.Invoke(context);

                    var process = await promise.WaitAsync(token);

                    process.Send(context, () =>
                    {
                        if (stopped)
                        {
                            return;
                        }

                        stopped = true;

                        lock (parent.sync)
                        {
                            parent.state = null;
                        }
                    });
                }
            }
        }

        private class ActorProcess : IVisitor
        {
            private readonly CommandQueue commands = new();

            private readonly ActorSystem system;
            private readonly IActor actor;
            private readonly IAddress address;

            private ActorProcess next;
            private bool isStopped;

            public ActorProcess(ActorSystem system, IActor actor, IAddress address)
            {
                this.system = system;
                this.actor = actor;
                this.address = address;
            }

            public void Send(IContext context, Action onStopped = null)
            {   
                commands.Schedule(new StrategyCommand(async () =>
                {
                    using (Context.Use(context))
                    {
                        if (await TryStopAsync(context, onStopped))
                        {
                            return;
                        }

                        if (await TrySendToNextProcessAsync(context, onStopped))
                        {
                            return;
                        }
                        
                        await ProcessAsync(context);
                    }
                }));
            }

            private async ValueTask<bool> TryStopAsync(IContext context, Action onStopped)
            {
                if (isStopped)
                {
                    return false;
                }
                
                var payload = context.Get<IPayload>();

                if (payload == null)
                {
                    return false;
                }

                payload.Accept(this);

                if (!isStopped)
                {
                    return false;
                }

                using (Disposable.Create(() => onStopped?.Invoke()))
                using (Disposable.Create(() => system.RemoveProcess(address)))
                await using (actor)
                {
                    var supervisor = context.Get<ISupervisor>();

                    if (supervisor != null)
                    {
                        await supervisor.OnStoppedAsync(context, system.token);
                    }

                    return true;
                }
            }

            private async ValueTask<bool> TrySendToNextProcessAsync(IContext context, Action onStopped)
            {
                if (!isStopped)
                {
                    return false;
                }

                onStopped?.Invoke();

                next ??= await system.GetOrCreateProcessAsync(address);

                next.Send(context);

                return true;
            }

            private async ValueTask ProcessAsync(IContext context)
            {
                Exception error = null;
                
                try
                {
                    await actor.OnReceiveAsync(context, system.token);
                }
                catch (Exception e)
                {
                    error = e;
                    
                    await actor.OnErrorAsync(context, e, system.token);
                }
                finally
                {
                    var supervisor = context.Get<ISupervisor>();

                    if (supervisor != null)
                    {
                        await supervisor.OnProcessedAsync(context, error, system.token);
                    }
                }
            }

            void IVisitor.Visit<T>(T value)
            {
                if (value is StoppingMessage)
                {
                    isStopped = true;
                }
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
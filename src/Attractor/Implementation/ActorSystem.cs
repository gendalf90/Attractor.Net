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

        async ValueTask<IActorRef> IActorSystem.GetRefAsync(IAddress address)
        {
            var process = await GetOrCreateProcessAsync(address);

            return process == null 
                ? null
                : new ActorRef(this, process, address);
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

        private async ValueTask<ActorProcess> GetOrCreateProcessAsync(IAddress address)
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

            return await completion.Task;
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
                        Completion.SetResult(null);
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

        private record ActorRef(ActorSystem System, ActorProcess Process, IAddress Address) : IActorRef
        {
            void IActorRef.Send(IPayload payload, Action<IContext> configuration)
            {
                ArgumentNullException.ThrowIfNull(payload, nameof(payload));
                
                System.token.ThrowIfCancellationRequested();
                
                var context = Context.Default();

                context.Set(Address);
                context.Set(payload);
                context.Set<IActorSystem>(System);
                
                configuration?.Invoke(context);

                Process.Send(context);
            }
        }

        private class ActorProcess
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

            public void Send(IContext context)
            {   
                commands.Schedule(new StrategyCommand(async () =>
                {
                    if (await TryStopAsync(context))
                    {
                        return;
                    }

                    if (await TrySendToNextProcessAsync(context))
                    {
                        return;
                    }
                    
                    await ProcessAsync(context);
                }));
            }

            private async ValueTask<bool> TryStopAsync(IContext context)
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

                if (!Payload.Match<StoppingMessage>(payload))
                {
                    return false;
                }

                using (Disposable.Create(() => system.RemoveProcess(address)))
                await using (actor)
                {
                    isStopped = true;

                    var supervisor = context.Get<ISupervisor>();

                    if (supervisor != null)
                    {
                        await supervisor.OnStoppedAsync(context, system.token);
                    }

                    return true;
                }
            }

            private async ValueTask<bool> TrySendToNextProcessAsync(IContext context)
            {
                if (!isStopped)
                {
                    return false;
                }

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
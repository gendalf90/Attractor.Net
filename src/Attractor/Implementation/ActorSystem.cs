using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections;

namespace Attractor.Implementation
{
    public static class ActorSystem
    {
        public static IActorSystem Create(CancellationToken token = default)
        {
            return new ActorSystemImpl(token);
        }
        
        private class ActorSystemImpl : IActorSystem
        {
            private readonly Dictionary<IAddress, ActorProcess> processes = new(AddressEqualityComparer.Default);
            private readonly LinkedList<ActorProcessBuilder> builders = new();
            private readonly CommandQueue<ICommand> commands = new();

            private readonly CancellationToken token;

            public ActorSystemImpl(CancellationToken token)
            {
                this.token = token;
            }

            IActorRef IActorSystem.Refer(IAddress address, bool onlyExist)
            {
                token.ThrowIfCancellationRequested();

                return new ActorRef(this, address, onlyExist);
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

            private Task<ActorProcess> GetOrCreateProcessAsync(IAddress address, bool onlyExist)
            {
                var completion = new TaskCompletionSource<ActorProcess>();

                commands.Schedule(new StrategyCommand(() =>
                {
                    if (processes.TryGetValue(address, out var result))
                    {
                        completion.SetResult(result);
                    }
                    else if (onlyExist)
                    {
                        completion.SetException(new InvalidOperationException());
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
                ActorSystemImpl System) : ICommand
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

                            process.Send(Context.System(), System.token);

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
                    
                    return default;
                }
            }

            private record StrategyCommand(Action Strategy) : ICommand
            {
                ValueTask ICommand.ExecuteAsync()
                {
                    Strategy();

                    return default;
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
                private readonly Task<ActorProcess> promise;

                public ActorRef(ActorSystemImpl system, IAddress address, bool onlyExist)
                {
                    promise = system.GetOrCreateProcessAsync(address, onlyExist);
                }
                
                async ValueTask IActorRef.SendAsync(IList context, CancellationToken token)
                {
                    var process = await promise.WaitAsync(token);

                    process.Send(context, token);
                }
            }

            private class ActorProcess : IActorProcess
            {
                private const long Starting = 0;
                private const long StopStarting = 1;
                private const long Processing = 2;
                private const long StopProcessing = 3;
                private const long Waiting = 4;
                private const long Stopping = 5;
                private const long Collecting = 6;
                
                private readonly PID pid = PID.Generate();
                private readonly CommandQueue<ProcessMessageCommand> commands = new();

                private readonly ActorSystemImpl system;
                private readonly IAddress address;
                private readonly IActor actor;

                private long state = Starting;

                public ActorProcess(ActorSystemImpl system, IAddress address, IActor actor)
                {
                    this.system = system;
                    this.address = address;
                    this.actor = actor;
                }

                ValueTask IActorRef.SendAsync(IList context, CancellationToken token)
                {
                    Send(context, token);

                    return default;
                }

                public void Send(IList context, CancellationToken token)
                {
                    ArgumentNullException.ThrowIfNull(context, nameof(context));

                    token.ThrowIfCancellationRequested();
                    system.token.ThrowIfCancellationRequested();

                    commands.Schedule(new ProcessMessageCommand
                    {
                        Process = this,
                        Context = context
                    });
                }

                private IActor DecorateFromContext(IList context)
                {
                    var decoratee = actor;
                    
                    foreach (var item in context)
                    {
                        if (item is IActorDecorator decorator)
                        {
                            decorator.Decorate(decoratee);

                            decoratee = decorator;
                        }
                    }

                    return decoratee;
                }

                private void BeforeProcessing()
                {
                    Interlocked.CompareExchange(ref state, Processing, Waiting);
                }

                private async ValueTask ProcessAsync(IList context)
                {
                    try
                    {
                        BeforeProcessing();
                        
                        context.Set<PID, IAddress, IActorSystem, IActorProcess>(pid, address, system, this);

                        var result = DecorateFromContext(context);
                        
                        await result.OnReceiveAsync(context, system.token);
                    }
                    finally
                    {
                        AfterProcessing();
                    }
                }

                private void AfterProcessing()
                {
                    if (Interlocked.CompareExchange(ref state, Collecting, Stopping) == Collecting)
                    {
                        return;
                    }
                    
                    if (Interlocked.CompareExchange(ref state, Waiting, Processing) == Processing ||
                        Interlocked.CompareExchange(ref state, Stopping, StopProcessing) == StopProcessing ||
                        Interlocked.CompareExchange(ref state, Waiting, Starting) == Starting ||
                        Interlocked.CompareExchange(ref state, Stopping, StopStarting) == StopStarting)
                    {
                        return;
                    }

                    system.RemoveProcess(address);
                }

                void IActorProcess.Stop()
                {
                    if (Interlocked.CompareExchange(ref state, Stopping, Waiting) == Waiting ||
                        Interlocked.CompareExchange(ref state, StopProcessing, Processing) == Processing ||
                        Interlocked.CompareExchange(ref state, StopStarting, Starting) == Starting)
                    {
                        Send(Context.System(), system.token);
                    }
                }

                bool IActorProcess.IsStarting()
                {
                    return Interlocked.Read(ref state) < Processing;
                }

                bool IActorProcess.IsProcessing()
                {
                    return Interlocked.Read(ref state) < Collecting;
                }

                bool IActorProcess.IsStopping()
                {
                    return Interlocked.Read(ref state) == Stopping;
                }

                bool IActorProcess.IsCollecting()
                {
                    return Interlocked.Read(ref state) == Collecting;
                }

                private readonly record struct ProcessMessageCommand(ActorProcess Process, IList Context) : ICommand
                {
                    ValueTask ICommand.ExecuteAsync()
                    {
                        return Process.ProcessAsync(Context);
                    }
                }
            }

            private class ActorProcessBuilder : IActorBuilder
            {
                private readonly ActorSystemImpl system;
                private readonly IAddressPolicy policy;

                private Func<IActor> defaultActorFactory = Actor.Empty;
                private Func<IActor, IActor> actorDecoratorFactory = _ => _;

                public ActorProcessBuilder(ActorSystemImpl system, IAddressPolicy policy)
                {
                    this.system = system;
                    this.policy = policy;
                }

                void IActorBuilder.Register<T>(Func<T> factory)
                {
                    defaultActorFactory = factory ?? throw new ArgumentNullException(nameof(factory));
                }

                void IActorBuilder.Decorate<T>(Func<T> factory)
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

                    process = new ActorProcess(system, address, actorDecoratorFactory(defaultActorFactory()));

                    return true;
                }

                private static Func<TResult, TResult> Decorate<TResult, TDecorator>(Func<TResult, TResult> resultFactory, Func<TDecorator> decoratorFactory) 
                    where TDecorator : class, TResult, IDecorator<TResult>
                {
                    return decoratee =>
                    {
                        var result = resultFactory(decoratee);
                        var decorator = decoratorFactory();

                        decorator.Decorate(result);

                        return decorator;
                    };
                }
            }
        }
    }
}
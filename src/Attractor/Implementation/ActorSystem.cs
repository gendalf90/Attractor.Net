using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

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
                ArgumentNullException.ThrowIfNull(address, nameof(address));
                
                token.ThrowIfCancellationRequested();

                return new ActorRef(this, address, onlyExist);
            }

            void IActorSystem.Register(IAddressPolicy policy, Action<IActorBuilder> configuration)
            {
                ArgumentNullException.ThrowIfNull(policy, nameof(policy));
                
                token.ThrowIfCancellationRequested();

                var builder = new ActorBuilder();

                configuration?.Invoke(builder);

                commands.Schedule(new StrategyCommand(() =>
                {
                    builders.AddFirst(new ActorProcessBuilder(policy, builder, this));
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
                
                async ValueTask IActorRef.SendAsync(IContext context, CancellationToken token)
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

                private readonly Action<KeyValuePair<object, object>> decorator;
                private readonly ActorSystemImpl system;
                private readonly IAddress address;
                private readonly IActor actor;

                private long state = Starting;
                private IActor decoratee;

                public ActorProcess(ActorSystemImpl system, IAddress address, IActor actor)
                {
                    this.system = system;
                    this.address = address;
                    this.actor = actor;

                    decorator = Decorate;
                }

                ValueTask IActorRef.SendAsync(IContext context, CancellationToken token)
                {
                    Send(context, token);

                    return default;
                }

                public void Send(IContext context, CancellationToken token)
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

                private void Decorate(IContext context)
                {
                    decoratee = actor;

                    context.ForEach(decorator);
                }

                private void Decorate(KeyValuePair<object, object> pair)
                {
                    if (pair.Value is IActorDecorator decorator)
                    {
                       decorator.Decorate(decoratee);

                        decoratee = decorator;
                    }
                }

                private void BeforeProcessing()
                {
                    Interlocked.CompareExchange(ref state, Processing, Waiting);
                }

                private async ValueTask ProcessAsync(IContext context)
                {
                    try
                    {
                        BeforeProcessing();
                        Decorate(context);
                        Init(context);
                        
                        await ReceiveAsync(context);
                    }
                    finally
                    {
                        AfterProcessing();
                    }
                }

                private void Init(IContext context)
                {
                    context.Set(pid);
                    context.Set(address);
                    context.Set<IActorSystem>(system);
                    context.Set<IActorProcess>(this);
                }

                private ValueTask ReceiveAsync(IContext context)
                {
                    return decoratee.OnReceiveAsync(context, system.token);
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

                bool IActorProcess.IsActive()
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

                private readonly record struct ProcessMessageCommand(ActorProcess Process, IContext Context) : ICommand
                {
                    ValueTask ICommand.ExecuteAsync()
                    {
                        return Process.ProcessAsync(Context);
                    }
                }
            }

            private record ActorProcessBuilder(IAddressPolicy Policy, ActorBuilder ActorBuilder, ActorSystemImpl ActorSystem)
            {
                public bool TryBuildProcess(IAddress address, out ActorProcess process)
                {
                    process = null;

                    if (!Policy.IsMatch(address))
                    {
                        return false;
                    }

                    process = new ActorProcess(ActorSystem, address, ActorBuilder.Build());

                    return true;
                }
            }
        }
    }
}
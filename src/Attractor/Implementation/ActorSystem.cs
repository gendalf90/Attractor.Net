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

        IActorRef IActorSystem.Ref(IAddress address)
        {
            return new ActorRef(address, this);
        }

        void IActorSystem.Register(IAddressPolicy policy, Action<IActorBuilder> configuration)
        {
            var builder = new ActorProcessBuilder(this, policy);

            configuration?.Invoke(builder);

            commands.Schedule(new SyncStrategyCommand(() =>
            {
                builders.AddLast(builder);
            }));
        }

        private async ValueTask<ActorProcess> GetOrCreateProcessAsync(IAddress address)
        {
            var completion = new TaskCompletionSource<ActorProcess>();

            commands.Schedule(new SyncStrategyCommand(() =>
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

        private class TryBuildProcessCommand : ICommand
        {
            private readonly IAddress address;
            private readonly LinkedListNode<ActorProcessBuilder> node;
            private readonly TaskCompletionSource<ActorProcess> completion;
            private readonly ActorSystem system;

            public TryBuildProcessCommand(
                TaskCompletionSource<ActorProcess> completion,
                LinkedListNode<ActorProcessBuilder> node,
                IAddress address,
                ActorSystem system)
            {
                this.completion = completion;
                this.node = node;
                this.address = address;
                this.system = system;
            }

            ValueTask ICommand.ExecuteAsync()
            {
                try
                {
                    if (system.processes.TryGetValue(address, out var result))
                    {
                        completion.SetResult(result);
                    }
                    else if (node == null)
                    {
                        completion.SetException(new InvalidOperationException());
                    }
                    else if (!node.Value.CanBuild(address))
                    {
                        system.commands.Schedule(new TryBuildProcessCommand(completion, node.Next, address, system));
                    }
                    else
                    {
                        var process = node.Value.BuildProcess(address);

                        process.Init();

                        system.processes.Add(address, process);

                        completion.SetResult(process);
                    }
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
                
                return ValueTask.CompletedTask;
            }
        }

        private void CloneProcess(IAddress address, ActorProcess process)
        {
            commands.Schedule(new SyncStrategyCommand(() =>
            {
                process.Init();
                process.Touch();

                processes[address] = process;
            }));
        }

        private void RemoveProcess(IAddress address)
        {
            commands.Schedule(new SyncStrategyCommand(() =>
            {
                processes.Remove(address);
            }));
        }

        private class SyncStrategyCommand : ICommand
        {
            private readonly Action strategy;

            public SyncStrategyCommand(Action strategy)
            {
                this.strategy = strategy;
            }
            
            ValueTask ICommand.ExecuteAsync()
            {
                strategy();

                return ValueTask.CompletedTask;
            }
        }

        private class ActorRef : IActorRef
        {
            private readonly IAddress address;
            private readonly ActorSystem system;
            
            private ActorProcess process;

            public ActorRef(IAddress address, ActorSystem system)
            {
                this.address = address;
                this.system = system;
            }

            async ValueTask IActorRef.SendAsync(IPayload payload, Action<IContext> configuration, CancellationToken token)
            {
                do
                {
                    var current = Interlocked.CompareExchange(ref process, null, null);
                    
                    try
                    {
                        if (current == null)
                        {
                            current = await system.GetOrCreateProcessAsync(address);

                            Interlocked.CompareExchange(ref process, current, null);
                        }

                        var context = Context.Default();

                        context.Set(address);
                        context.Set(payload);

                        configuration?.Invoke(context);

                        await current.SendAsync(context, token);

                        return;
                    }
                    catch (OperationCanceledException cancel) when (current.CheckToken(cancel.CancellationToken))
                    {
                        try
                        {
                            await current.WaitAsync(token);
                        }
                        finally
                        {
                            Interlocked.CompareExchange(ref process, null, current);
                        }
                    }
                }
                while (true);
            }
        }

        private class ActorProcess : Particle, IActorProcess
        {
            private const long Started = 0;
            private const long Cancelled = 1;
            private const long Cloned = 2;
            private const long Finished = 3;

            
            private readonly ActorSystem system;
            private readonly ActorProcessBuilder builder;
            private readonly IAddress address;
            private readonly ISupervisor supervisor;
            private readonly IMailbox mailbox;
            private readonly IContext context;

            private IActor actor;
            private CancellationTokenSource cancellation;
            private TaskCompletionSource completion;
            private IAsyncDisposable disposing;
            private long state;

            public ActorProcess(
                ActorSystem system,
                ActorProcessBuilder builder,
                IAddress address,
                IMailbox mailbox, 
                ISupervisor supervisor,
                IContext context)
            {
                this.system = system;
                this.builder = builder;
                this.address = address;
                this.mailbox = mailbox;
                this.supervisor = supervisor;
                this.context = context;
            }

            public bool CheckToken(CancellationToken token)
            {
                return cancellation.Token == token;
            }

            public async ValueTask WaitAsync(CancellationToken token = default)
            {
                await completion.Task.WaitAsync(token);
            }

            public async ValueTask SendAsync(IContext context, CancellationToken token)
            {   
                using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token, token);
                
                source.Token.ThrowIfCancellationRequested();

                await mailbox.SendAsync(context, source.Token);
                
                Touch();
            }

            public void Init()
            {
                cancellation = CancellationTokenSource.CreateLinkedTokenSource(system.token);
                completion = new TaskCompletionSource();

                context.Set(address);
                context.Set<IActorProcess>(this);

                var registration = cancellation.Token.Register(Touch);

                disposing = Disposable.Create(async () =>
                {
                    using (Disposable.Create(completion.SetResult))
                    using (Disposable.Create(CleanOrUpdateProcesses))
                    await using (actor)
                    {
                        registration.Dispose();
                    }
                });
            }

            private void CleanOrUpdateProcesses()
            {
                if (Interlocked.Exchange(ref state, Finished) == Cloned)
                {
                    system.CloneProcess(address, new ActorProcess(system, builder, address, mailbox, supervisor, context));
                }
                else
                {
                    system.RemoveProcess(address);
                }
            }

            void IActorProcess.Clone()
            {
                Interlocked.Exchange(ref state, Cloned);

                cancellation.Cancel();
            }

            void IActorProcess.Awake(TimeSpan delay, CancellationToken token)
            {
                if (delay == default)
                {
                    Touch();

                    return;
                }
                
                var delaySource = new CancellationTokenSource(delay);
                var cancelSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token, token);

                delaySource.Token.Register(() =>
                {
                    cancelSource.Dispose();
                    delaySource.Dispose();

                    Touch();
                });

                cancelSource.Token.Register(() =>
                {
                    delaySource.Dispose();
                    cancelSource.Dispose();
                });
            }

            protected override async ValueTask ProcessAsync()
            {
                try
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    
                    while (await mailbox.ReceiveAsync(cancellation.Token) is var messageContext)
                    {
                        actor ??= builder.BuildActor();

                        messageContext.Set<IActorProcess>(this);

                        await actor.OnReceiveAsync(messageContext, cancellation.Token);
                    }
                }
                catch (Exception e)
                {
                    if (!TryFinish())
                    {
                        return;
                    }

                    cancellation.Cancel();
                    
                    await using (disposing)
                    {
                        await supervisor.OnFaultAsync(context, e, system.token);
                    }
                }
            }

            private bool TryFinish()
            {
                return Interlocked.CompareExchange(ref state, Cancelled, Started) != Finished;
            }
        }

        private class ActorProcessBuilder : IActorBuilder
        {
            private readonly ActorSystem system;
            private readonly IAddressPolicy policy;

            private Func<IActor, IActor> actorDecoratorFactory = _ => _;
            private Func<IMailbox, IMailbox> mailboxDecoratorFactory = _ => _;
            private Func<ISupervisor, ISupervisor> supervisorDecoratorFactory = _ => _;

            private Func<IMailbox> defaultMailboxFactory = Mailbox.Default;
            private Func<ISupervisor> defaultSupervisorFactory = Supervisor.Empty;
            private Func<IActor> defaultActorFactory = Actor.Empty;

            public ActorProcessBuilder(ActorSystem system, IAddressPolicy policy)
            {
                this.system = system;
                this.policy = policy;
            }

            void IActorBuilder.DecorateActor<T>(Func<T> factory)
            {
                ArgumentNullException.ThrowIfNull(factory, nameof(factory));

                actorDecoratorFactory = Decorate(actorDecoratorFactory, factory);
            }

            void IActorBuilder.DecorateMailbox<T>(Func<T> factory)
            {
                ArgumentNullException.ThrowIfNull(factory, nameof(factory));

                mailboxDecoratorFactory = Decorate(mailboxDecoratorFactory, factory);
            }

            void IActorBuilder.DecorateSupervisor<T>(Func<T> factory)
            {
                ArgumentNullException.ThrowIfNull(factory, nameof(factory));

                supervisorDecoratorFactory = Decorate(supervisorDecoratorFactory, factory);
            }

            public bool CanBuild(IAddress address)
            {
                return policy.IsMatch(address);
            }

            public ActorProcess BuildProcess(IAddress address)
            {
                var mailbox = mailboxDecoratorFactory(defaultMailboxFactory());
                var supervisor = supervisorDecoratorFactory(defaultSupervisorFactory());
                var context = Context.Default();
                
                return new ActorProcess(system, this, address, mailbox, supervisor, context);
            }

            public IActor BuildActor()
            {
                return actorDecoratorFactory(defaultActorFactory());
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

            void IActorBuilder.RegisterActor<T>(Func<T> factory)
            {
                defaultActorFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            }

            void IActorBuilder.RegisterMailbox<T>(Func<T> factory)
            {
                defaultMailboxFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            }

            void IActorBuilder.RegisterSupervisor<T>(Func<T> factory)
            {
                defaultSupervisorFactory = factory ?? throw new ArgumentNullException(nameof(factory));
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation;

public static class System
{
    public static void AddSystem(this IActorBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        builder.Decorate(() => new SystemDecorator());
    }

    public static void Register(this IActorRef systemRef, IAddressPolicy policy, IProps properties)
    {
        ArgumentNullException.ThrowIfNull(systemRef, nameof(systemRef));
        ArgumentNullException.ThrowIfNull(policy, nameof(policy));
        ArgumentNullException.ThrowIfNull(properties, nameof(properties));

        systemRef.Send(Message.Value(new Registration(policy, properties)));
    }

    public static IActorRef GetOrRun(this IActorRef systemRef, IAddress address)
    {
        ArgumentNullException.ThrowIfNull(systemRef, nameof(systemRef));
        ArgumentNullException.ThrowIfNull(address, nameof(address));

        systemRef.Send(Message.Value(new Runner(address)));
    }

    private record Registration(IAddressPolicy Policy, IProps Properties);

    private record Runner(IAddress Address);

    private class SystemDecorator : IActor, IDecorator<IActor>
    {
        private readonly LinkedList<Registration> registrations = new();
        private readonly Dictionary<IAddress, IActorRef> children = new();

        private IActor decoratee;

        void IDecorator<IActor>.Decorate(IActor value)
        {
            decoratee = value;
        }

        async ValueTask IAsyncDisposable.DisposeAsync()
        {
            await decoratee.DisposeAsync();
        }

        async Task IActor.OnReceiveAsync(IContext context, CancellationToken token)
        {
            var registration = context.Get<Registration>();
            var runner = context.Get<Runner>();

            if (registration != null)
            {
                commandQueue.Schedule(CreateRegistrationCommand(registration));
            }

            await decoratee.OnReceiveAsync(context, token);
        }

        async Task IActor.OnStartAsync(IContext context, CancellationToken token)
        {
            await decoratee.OnStartAsync(context, token);
        }

        private ICommand CreateRegistrationCommand(Registration registration) => Command.From(() =>
        {
            registrations.AddFirst(registration);
        });

        private ICommand StartProcessCommand(AwaiterFeature awaiter, IAddress address, StartMessage message) => Command.From(() =>
        {
            if (cancellation.IsCancellationRequested)
            {
                awaiter?.SetCanceled(cancellation.Token);
            }
            else if (children.TryGetValue(address, out var result))
            {
                message.Complete(result);
                awaiter?.SetResult();
            }
            else
            {
                commands.Schedule(BuildProcessCommand(awaiter, address, message, registrations.First));
            }
        });

        private ICommand BuildProcessCommand(AwaiterFeature awaiter, IAddress address, StartMessage message, LinkedListNode<RegisterMessage> node)
        {
            return Command.From(() =>
            {
                try
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        awaiter?.SetCanceled(cancellation.Token);
                    }
                    else if (children.TryGetValue(address, out var result))
                    {
                        message.Complete(result);
                        awaiter?.SetResult();
                    }
                    else if (node == null)
                    {
                        awaiter?.SetException(new NullReferenceException());
                    }
                    else if (node.Value.IsMatch(address))
                    {
                        var disposing = Disposable.CreateAsync(async () =>
                        {
                            await commands.ScheduleAsync(() =>
                            {
                                children.Remove(address);
                            });
                        });

                        var process = node.Value.Build(address, disposing, cancellation.Token);

                        children.Add(address, process);
                        process.Start();
                        message.Complete(process);
                        awaiter?.SetResult();
                    }
                    else
                    {
                        commands.Schedule(BuildProcessCommand(awaiter, address, message, node.Next));
                    }
                }
                catch (Exception ex)
                {
                    awaiter?.SetException(ex);
                }
            });
        }
    }

    private class SystemActorRef : IActorRef
    {
        private volatile IActorRef actorRef;

        public SystemActorRef(IActorRef actorRef)
        {
            this.actorRef = actorRef;
        }

        public void Send(IMessage message)
        {
            actorRef.Send(message);
        }

        public void Set(IActorRef actorRef)
        {
            this.actorRef = actorRef;
        }
    }



    public static ISystem Create(CancellationToken token = default)
    {
        return new SystemProcess(token);
    }

    private class SystemProcess : ISystem
    {
        private readonly CommandQueue commands = new();
        private readonly LinkedList<RegisterMessage> registrations = new();
        private readonly Dictionary<IAddress, Process> children = new();

        private State state;
        private CancellationTokenSource cancellation;
        private CancellationTokenRegistration registration;
        private Task disposingTask;

        public SystemProcess(CancellationToken token)
        {
            state = State.Started;
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            registration = cancellation.Token.Register(() => commands.Schedule(RunDisposingCommand));
        }

        public void Send(IContext context)
        {
            var awaiter = context.Get<AwaiterFeature>();
            var registration = context.Get<RegisterMessage>();
            var start = context.Get<StartMessage>();
            var address = context.Get<IAddress>();

            if (registration != null)
            {
                commands.Schedule(RegisterCommand(awaiter, registration));
            }

            if (start != null && address != null)
            {
                commands.Schedule(StartProcessCommand(awaiter, address, start));
            }
        }

        private ICommand RegisterCommand(AwaiterFeature awaiter, RegisterMessage message) => Command.From(() =>
        {
            if (cancellation.IsCancellationRequested)
            {
                awaiter?.SetCanceled(cancellation.Token);
            }
            else
            {
                registrations.AddFirst(message);
                awaiter?.SetResult();
            }
        });

        private ICommand StartProcessCommand(AwaiterFeature awaiter, IAddress address, StartMessage message) => Command.From(() =>
        {
            if (cancellation.IsCancellationRequested)
            {
                awaiter?.SetCanceled(cancellation.Token);
            }
            else if (children.TryGetValue(address, out var result))
            {
                message.Complete(result);
                awaiter?.SetResult();
            }
            else
            {
                commands.Schedule(BuildProcessCommand(awaiter, address, message, registrations.First));
            }
        });

        private ICommand BuildProcessCommand(AwaiterFeature awaiter, IAddress address, StartMessage message, LinkedListNode<RegisterMessage> node)
        {
            return Command.From(() =>
            {
                try
                {
                    if (cancellation.IsCancellationRequested)
                    {
                        awaiter?.SetCanceled(cancellation.Token);
                    }
                    else if (children.TryGetValue(address, out var result))
                    {
                        message.Complete(result);
                        awaiter?.SetResult();
                    }
                    else if (node == null)
                    {
                        awaiter?.SetException(new NullReferenceException());
                    }
                    else if (node.Value.IsMatch(address))
                    {
                        var disposing = Disposable.CreateAsync(async () =>
                        {
                            await commands.ScheduleAsync(() =>
                            {
                                children.Remove(address);
                            });
                        });

                        var process = node.Value.Build(address, disposing, cancellation.Token);

                        children.Add(address, process);
                        process.Start();
                        message.Complete(process);
                        awaiter?.SetResult();
                    }
                    else
                    {
                        commands.Schedule(BuildProcessCommand(awaiter, address, message, node.Next));
                    }
                }
                catch (Exception ex)
                {
                    awaiter?.SetException(ex);
                }
            });
        }

        public async ValueTask DisposeAsync()
        {
            await commands.ScheduleAsync(RunDisposingCommand);
            await disposingTask;
        }

        private ICommand RunDisposingCommand => Command.From(() =>
        {
            if (state != State.Disposing)
            {
                state = State.Disposing;
                cancellation.Cancel();
                disposingTask = Task.Run(RunDisposingAsync);
            }
        });

        private async Task RunDisposingAsync()
        {
            using (cancellation)
            using (registration)
            {
                await Task.WhenAll(children.Values.Select(async process => await process.DisposeAsync()));
            }
        }
    }
}
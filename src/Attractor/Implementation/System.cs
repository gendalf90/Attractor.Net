using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation;

public static class System
{
    public static void AddSystem(this IActorBuilder builder, DecorateConfigure configuration = null)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));

        var registrations = new LinkedList<RegisterMessage>();
        var children = new Dictionary<IAddress, IActorProcess>(Address.EqualityComparer);

        builder.OnReceive<RegisterMessage>((message, _) =>
        {
            registrations.AddFirst(message);
        });

        builder.OnReceive<RunMessage>((message, context) =>
        {
            if (children.TryGetValue(message.Address, out var actorProcess))
            {
                message.SystemActorRef.SetActorRef(actorProcess);

                return;
            }

            var actorRegistration = registrations.FirstOrDefault(registration => registration.Policy.IsMatch(message.Address));

            if (actorRegistration == null)
            {
                var nullRef = CreateNullRef();

                message.SystemActorRef.SetActorRef(nullRef);

                return;
            }

            var systemProcess = context.Get<IActorProcess>();
            var actorProperties = Props.From(builder =>
            {
                builder.Use(message.Address);

                if (configuration == null)
                {
                    actorRegistration.Properties.Configure(builder);
                }
                else
                {
                    configuration(actorRegistration.Properties.Configure, builder);
                }

                builder.OnDispose(() => systemProcess.Send(Message.Value(new DisposeMessage(message.Address))));
            });

            actorProcess = Actor.Run(actorProperties, systemProcess.Cancellation);

            children.Add(message.Address, actorProcess);

            message.SystemActorRef.SetActorRef(actorProcess);
        });

        builder.OnReceive<DisposeMessage>((message, _) =>
        {
            children.Remove(message.Address);
        });

        builder.OnDispose(async () =>
        {
            foreach (var child in children.Values)
            {
                child.Dispose();
            }
            
            await Task.WhenAll(children.Values.Select(actor => actor.Completion));
        });
    }

    public static void Register(this IActorRef systemRef, IAddressPolicy policy, IProps properties)
    {
        ArgumentNullException.ThrowIfNull(systemRef, nameof(systemRef));
        ArgumentNullException.ThrowIfNull(policy, nameof(policy));
        ArgumentNullException.ThrowIfNull(properties, nameof(properties));

        systemRef.Send(Message.Value(new RegisterMessage(policy, properties)));
    }

    public static IActorRef Run(this IActorRef systemRef, IAddress address)
    {
        ArgumentNullException.ThrowIfNull(systemRef, nameof(systemRef));
        ArgumentNullException.ThrowIfNull(address, nameof(address));

        var actorRef = new SystemActorRefDecorator();

        var request = systemRef.Send(Message.Value(new RunMessage(address, actorRef)));

        actorRef.SetCompletion(request.Completion);

        return actorRef;
    }

    private record RegisterMessage(IAddressPolicy Policy, IProps Properties);

    private record RunMessage(IAddress Address, SystemActorRefDecorator SystemActorRef);

    private record DisposeMessage(IAddress Address);

    private class SystemActorRefDecorator : IActorRef
    {
        private IActorRef actorRef;
        private Task completion;

        IRequest IActorRef.Send(IMessage message, CancellationToken token)
        {
            return new SystemRequestDecorator(this, message, token);
        }

        public void SetActorRef(IActorRef actorRef)
        {
            this.actorRef = actorRef;
        }

        public void SetCompletion(Task completion)
        {
            this.completion = completion;
        }

        private class SystemRequestDecorator : IRequest
        {
            private readonly CancellationTokenSource cancellation = new();
            private readonly SystemActorRefDecorator systemActorRef;

            public SystemRequestDecorator(SystemActorRefDecorator systemActorRef, IMessage message, CancellationToken token)
            {
                this.systemActorRef = systemActorRef;
                
                Completion = SendAsync(message, token);
            }
            
            public Task Completion { get; }

            private async Task SendAsync(IMessage message, CancellationToken token)
            {
                await systemActorRef.completion;

                var request = systemActorRef.actorRef.Send(message, token);

                request.Cancellation.Register(cancellation.Cancel);

                await request.Completion;
            }

            public CancellationToken Cancellation => cancellation.Token;
        }
    }

    private static IActorRef CreateNullRef()
    {
        return Actor.Run(Props.From(builder => builder.OnReceive((_, _) => throw new NullReferenceException())));
    }
}
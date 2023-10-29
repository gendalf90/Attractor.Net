using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public static class Actor
    {
        private static readonly IActor empty = new Instance(null);
        
        public static IActorDecorator FromStrategy(OnReceiveDecorator onReceive)
        {
            return new Decorator(onReceive ?? throw new ArgumentNullException(nameof(onReceive)));
        }

        public static IActor FromPayload<T>(OnReceive<T> onReceive)
        {
            return new Instance<T>(onReceive ?? throw new ArgumentNullException(nameof(onReceive)));
        }

        public static IActor FromStrategy(OnReceive onReceive)
        {
            return new Instance(onReceive ?? throw new ArgumentNullException(nameof(onReceive)));
        }

        public static IActor FromBuilder(Action<IActorBuilder> configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));

            var builder = new ActorBuilder();

            configuration(builder);

            return builder.Build();
        }

        public static IActor Empty()
        {
            return empty;
        }

        private class Decorator : IActorDecorator
        {
            private readonly OnReceiveDecorator onReceive;
            private readonly OnReceive onActorReceive;

            private IActor decoratee;

            public Decorator(OnReceiveDecorator onReceive)
            {
                this.onReceive = onReceive;

                onActorReceive = OnActorReceiveAsync;
            }

            void IDecorator<IActor>.Decorate(IActor value)
            {
                decoratee = value;
            }

            private ValueTask OnActorReceiveAsync(IList context, CancellationToken token)
            {
                return decoratee == null ? default : decoratee.OnReceiveAsync(context, token);
            }

            ValueTask IActor.OnReceiveAsync(IList context, CancellationToken token)
            {
                return onReceive != null ? onReceive(onActorReceive, context, token) : onActorReceive(context, token);
            }
        }

        private record Instance(OnReceive OnReceive) : IActor
        {
            ValueTask IActor.OnReceiveAsync(IList context, CancellationToken token)
            {
                return OnReceive != null ? OnReceive(context, token) : default;
            }
        }

        private record Instance<TPayload>(OnReceive<TPayload> OnReceive) : IActor, IVisitor
        {
            private TPayload acceptedValue;
            private bool isAccepted;
            
            ValueTask IActor.OnReceiveAsync(IList context, CancellationToken token)
            {
                if (!TryAccept(context))
                {
                    return default;
                }

                return OnReceive != null ? OnReceive(acceptedValue, token) : default;
            }

            private bool TryAccept(IList context)
            {
                isAccepted = false;
                acceptedValue = default;
                
                var payload = context.Find<IPayload>();

                if (payload == null)
                {
                    return false;
                }

                payload.Accept(this);

                return isAccepted;
            }

            void IVisitor.Visit<TValue>(TValue value)
            {
                if (value is TPayload result)
                {
                    acceptedValue = result;
                    isAccepted = true;
                }
            }
        }

        public static IActorDecorator ResendCollector(IMessageFilter filter = null)
        {
            IActorRef collector = null;

            async ValueTask CollectAsync(IList context, CancellationToken token)
            {
                var process = context.Find<IActorProcess>();

                if (!process.IsCollecting())
                {
                    return;
                }

                if (filter != null && !await filter.IsMatchAsync(context, token))
                {
                    return;
                }

                if (collector == null)
                {
                    var (system, address) = context.Find<IActorSystem, IAddress>();

                    collector = system.Refer(address);
                }

                await collector.SendAsync(context, token);
            };
            
            return FromStrategy(async (next, context, token) =>
            {
                await CollectAsync(context, token);
                
                await next(context, token);
            });
        }
    }
}
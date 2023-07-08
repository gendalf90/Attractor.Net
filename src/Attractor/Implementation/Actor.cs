using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public static class Actor
    {
        private static readonly IActor empty = new Instance(null, null);
        
        public static IActorDecorator FromStrategy(
            OnReceiveDecorator onReceive = null,
            OnDisposeDecorator onDispose = null)
        {
            return new Decorator(onReceive, onDispose);
        }

        public static IActor FromString(
            Func<string, CancellationToken, ValueTask> onReceive = null,
            Func<ValueTask> onDispose = null)
        {
            return new Instance<string>(onReceive, onDispose);
        }

        public static IActor FromBytes(
            Func<byte[], CancellationToken, ValueTask> onReceive = null,
            Func<ValueTask> onDispose = null)
        {
            return new Instance<byte[]>(onReceive, onDispose);
        }

        public static IActor FromStrategy(
            Func<IContext, CancellationToken, ValueTask> onReceive = null,
            Func<ValueTask> onDispose = null)
        {
            return new Instance(onReceive, onDispose);
        }

        public static IActor Empty()
        {
            return empty;
        }

        private record Decorator(OnReceiveDecorator OnReceive, OnDisposeDecorator OnDispose) : IActorDecorator
        {
            private Func<IContext, CancellationToken, ValueTask> onReceive;
            private Func<ValueTask> onDispose;

            void IDecorator<IActor>.Decorate(IActor value)
            {
                onReceive = value.OnReceiveAsync;
                onDispose = value.DisposeAsync;
            }

            ValueTask IAsyncDisposable.DisposeAsync()
            {
                return OnDispose != null ? OnDispose(onDispose) : onDispose();
            }

            ValueTask IActor.OnReceiveAsync(IContext context, CancellationToken token)
            {
                return OnReceive != null ? OnReceive(onReceive, context, token) : onReceive(context, token);
            }
        }

        private record Instance(
            Func<IContext, CancellationToken, ValueTask> OnReceive,
            Func<ValueTask> OnDispose) : IActor
        {
            ValueTask IAsyncDisposable.DisposeAsync()
            {
                return OnDispose != null ? OnDispose() : default;
            }

            ValueTask IActor.OnReceiveAsync(IContext context, CancellationToken token)
            {
                return OnReceive != null ? OnReceive(context, token) : default;
            }
        }

        private record Instance<TType>(
            Func<TType, CancellationToken, ValueTask> OnReceive,
            Func<ValueTask> OnDispose) : IActor, IVisitor
        {
            private TType acceptedValue;
            private bool isAccepted;
            
            ValueTask IAsyncDisposable.DisposeAsync()
            {
                return OnDispose != null ? OnDispose() : default;
            }

            ValueTask IActor.OnReceiveAsync(IContext context, CancellationToken token)
            {
                if (!TryAccept(context))
                {
                    return default;
                }

                return OnReceive != null ? OnReceive(acceptedValue, token) : default;
            }

            private bool TryAccept(IContext context)
            {
                isAccepted = false;
                
                var payload = context.Get<IPayload>();

                if (payload == null)
                {
                    return false;
                }

                payload.Accept(this);

                return isAccepted;
            }

            void IVisitor.Visit<TValue>(TValue value)
            {
                if (value is TType result)
                {
                    acceptedValue = result;
                    isAccepted = true;
                }
            }
        }
    }
}
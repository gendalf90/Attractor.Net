using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public static class Actor
    {
        private static readonly IActor empty = new Instance(null, null, null);
        
        public static IActorDecorator FromStrategy(
            Func<Func<IContext, CancellationToken, ValueTask>, IContext, CancellationToken, ValueTask> onReceive = null,
            Func<Func<IContext, Exception, CancellationToken, ValueTask>, IContext, Exception, CancellationToken, ValueTask> onError = null,
            Func<Func<ValueTask>, ValueTask> onDispose = null)
        {
            return new Decorator(onReceive, onError, onDispose);
        }

        public static IActor FromString(
            Func<string, CancellationToken, ValueTask> onReceive = null,
            Func<string, Exception, CancellationToken, ValueTask> onError = null,
            Func<ValueTask> onDispose = null)
        {
            return new Instance<string>(onReceive, onError, onDispose);
        }

        public static IActor FromBytes(
            Func<byte[], CancellationToken, ValueTask> onReceive = null,
            Func<byte[], Exception, CancellationToken, ValueTask> onError = null,
            Func<ValueTask> onDispose = null)
        {
            return new Instance<byte[]>(onReceive, onError, onDispose);
        }

        public static IActor FromStrategy(
            Func<IContext, CancellationToken, ValueTask> onReceive = null,
            Func<IContext, Exception, CancellationToken, ValueTask> onError = null,
            Func<ValueTask> onDispose = null)
        {
            return new Instance(onReceive, onError, onDispose);
        }

        public static IActor Empty()
        {
            return empty;
        }

        private record Decorator(
            Func<Func<IContext, CancellationToken, ValueTask>, IContext, CancellationToken, ValueTask> OnReceive,
            Func<Func<IContext, Exception, CancellationToken, ValueTask>, IContext, Exception, CancellationToken, ValueTask> OnError,
            Func<Func<ValueTask>, ValueTask> OnDispose) : IActorDecorator
        {
            private IActor value;

            void IDecorator<IActor>.Decorate(IActor value)
            {
                this.value = value;
            }

            ValueTask IAsyncDisposable.DisposeAsync()
            {
                return OnDispose != null ? OnDispose(value.DisposeAsync) : value.DisposeAsync();
            }

            ValueTask IActor.OnErrorAsync(IContext context, Exception error, CancellationToken token)
            {
                return OnError != null ? OnError(value.OnErrorAsync, context, error, token) : value.OnErrorAsync(context, error, token);
            }

            ValueTask IActor.OnReceiveAsync(IContext context, CancellationToken token)
            {
                return OnReceive != null ? OnReceive(value.OnReceiveAsync, context, token) : value.OnReceiveAsync(context, token);
            }
        }

        private record Instance(
            Func<IContext, CancellationToken, ValueTask> OnReceive,
            Func<IContext, Exception, CancellationToken, ValueTask> OnError,
            Func<ValueTask> OnDispose) : IActor
        {
            ValueTask IAsyncDisposable.DisposeAsync()
            {
                return OnDispose != null ? OnDispose() : default;
            }

            ValueTask IActor.OnErrorAsync(IContext context, Exception error, CancellationToken token)
            {
                return OnError != null ? OnError(context, error, token) : default;
            }

            ValueTask IActor.OnReceiveAsync(IContext context, CancellationToken token)
            {
                return OnReceive != null ? OnReceive(context, token) : default;
            }
        }

        private record Instance<TType>(
            Func<TType, CancellationToken, ValueTask> OnReceive,
            Func<TType, Exception, CancellationToken, ValueTask> OnError,
            Func<ValueTask> OnDispose) : IActor, IVisitor
        {
            private TType acceptedValue;
            private bool isAccepted;
            
            ValueTask IAsyncDisposable.DisposeAsync()
            {
                return OnDispose != null ? OnDispose() : default;
            }

            ValueTask IActor.OnErrorAsync(IContext context, Exception error, CancellationToken token)
            {
                if (!TryAccept(context))
                {
                    return default;
                }

                return OnError != null ? OnError(acceptedValue, error, token) : default;
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
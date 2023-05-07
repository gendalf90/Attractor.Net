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

        private class Decorator : IActorDecorator
        {
            private readonly Func<Func<IContext, CancellationToken, ValueTask>, IContext, CancellationToken, ValueTask> onReceive;
            private readonly Func<Func<IContext, Exception, CancellationToken, ValueTask>, IContext, Exception, CancellationToken, ValueTask> onError;
            private readonly Func<Func<ValueTask>, ValueTask> onDispose;

            private IActor value;

            public Decorator(
                Func<Func<IContext, CancellationToken, ValueTask>, IContext, CancellationToken, ValueTask> onReceive,
                Func<Func<IContext, Exception, CancellationToken, ValueTask>, IContext, Exception, CancellationToken, ValueTask> onError,
                Func<Func<ValueTask>, ValueTask> onDispose)
            {
                this.onReceive = onReceive;
                this.onError = onError;
                this.onDispose = onDispose;
            }

            void IDecorator<IActor>.Decorate(IActor value)
            {
                this.value = value;
            }

            ValueTask IAsyncDisposable.DisposeAsync()
            {
                return onDispose != null ? onDispose(value.DisposeAsync) : value.DisposeAsync();
            }

            ValueTask IActor.OnErrorAsync(IContext context, Exception error, CancellationToken token)
            {
                return onError != null ? onError(value.OnErrorAsync, context, error, token) : value.OnErrorAsync(context, error, token);
            }

            ValueTask IActor.OnReceiveAsync(IContext context, CancellationToken token)
            {
                return onReceive != null ? onReceive(value.OnReceiveAsync, context, token) : value.OnReceiveAsync(context, token);
            }
        }

        private class Instance : IActor
        {
            private readonly Func<IContext, CancellationToken, ValueTask> onReceive;
            private readonly Func<IContext, Exception, CancellationToken, ValueTask> onError;
            private readonly Func<ValueTask> onDispose;

            public Instance(
                Func<IContext, CancellationToken, ValueTask> onReceive,
                Func<IContext, Exception, CancellationToken, ValueTask> onError,
                Func<ValueTask> onDispose)
            {
                this.onReceive = onReceive;
                this.onError = onError;
                this.onDispose = onDispose;
            }

            ValueTask IAsyncDisposable.DisposeAsync()
            {
                return onDispose != null ? onDispose() : default;
            }

            ValueTask IActor.OnErrorAsync(IContext context, Exception error, CancellationToken token)
            {
                return onError != null ? onError(context, error, token) : default;
            }

            ValueTask IActor.OnReceiveAsync(IContext context, CancellationToken token)
            {
                return onReceive != null ? onReceive(context, token) : default;
            }
        }
    }
}
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public static class Actor
    {
        private static readonly Stub stub = new();
        
        public static IActorDecorator FromStrategy(
            Func<Func<IContext, CancellationToken, ValueTask>, IContext, CancellationToken, ValueTask> onReceive = null,
            Func<Func<ValueTask>, ValueTask> onDispose = null)
        {
            return new Decorator(onReceive, onDispose);
        }

        public static IActor FromStrategy(
            Func<IContext, CancellationToken, ValueTask> onReceive = null,
            Func<ValueTask> onDispose = null)
        {
            return new Instance(onReceive, onDispose);
        }

        public static IActor Empty()
        {
            return stub;
        }

        private class Decorator : IActorDecorator
        {
            private readonly Func<Func<IContext, CancellationToken, ValueTask>, IContext, CancellationToken, ValueTask> onReceive;
            private readonly Func<Func<ValueTask>, ValueTask> onDispose;

            private IActor value;

            public Decorator(
                Func<Func<IContext, CancellationToken, ValueTask>, IContext, CancellationToken, ValueTask> onReceive,
                Func<Func<ValueTask>, ValueTask> onDispose)
            {
                this.onReceive = onReceive;
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

            ValueTask IActor.OnReceiveAsync(IContext context, CancellationToken token)
            {
                return onReceive != null ? onReceive(value.OnReceiveAsync, context, token) : value.OnReceiveAsync(context, token);
            }
        }

        private class Instance : IActor
        {
            private readonly Func<IContext, CancellationToken, ValueTask> onReceive;
            private readonly Func<ValueTask> onDispose;

            public Instance(
                Func<IContext, CancellationToken, ValueTask> onReceive,
                Func<ValueTask> onDispose)
            {
                this.onReceive = onReceive;
                this.onDispose = onDispose;
            }

            ValueTask IAsyncDisposable.DisposeAsync()
            {
                return onDispose != null ? onDispose() : default;
            }

            ValueTask IActor.OnReceiveAsync(IContext context, CancellationToken token)
            {
                return onReceive != null ? onReceive(context, token) : default;
            }
        }

        private class Stub : IActor
        {
            ValueTask IAsyncDisposable.DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }

            ValueTask IActor.OnReceiveAsync(IContext context, CancellationToken token)
            {
                return ValueTask.CompletedTask;
            }
        }
    }
}
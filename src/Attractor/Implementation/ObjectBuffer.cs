using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public static class ObjectBuffer
    {
        public static IPayload Payload<T>(T value)
        {
            return new InternalObjectBuffer<T>(value);
        }

        public static IStreamHandler Process<T>(Func<T, CancellationToken, ValueTask> strategy)
        {
            return new InternalStrategyHandler<T>(strategy);
        }

        public static IStreamHandler Process<T>(Action<T> strategy)
        {
            return new InternalStrategyHandler<T>((buffer, _) =>
            {
                strategy(buffer);

                return ValueTask.CompletedTask;
            });
        }

        private class InternalObjectBuffer<T> : IPayload
        {
            private readonly T value;

            public InternalObjectBuffer(T value)
            {
                this.value = value;
            }

            public void Accept(IVisitor visitor)
            {
                visitor.Visit(value);
            }

            public IPayload Clone()
            {
                return this;
            }
        }

        private class ObjectValueVisitor<TResult> : IVisitor
        {
            public void Visit<TValue>(TValue value)
            {
                Result = value is TResult result
                    ? TryResult<TResult>.True(result)
                    : TryResult<TResult>.False();
            }

            public TryResult<TResult> Result { get; private set; }
        }

        private class InternalStrategyHandler<T> : IStreamHandler
        {
            private readonly Func<T, CancellationToken, ValueTask> strategy;

            public InternalStrategyHandler(Func<T, CancellationToken, ValueTask> strategy)
            {
                this.strategy = strategy;
            }

            public ValueTask OnReceiveAsync(IContext context)
            {
                var request = context.Get<IRequest>();
                var visitor = new ObjectValueVisitor<T>();

                request.Accept(visitor);

                if (!visitor.Result.Success)
                {
                    return ValueTask.CompletedTask;
                }

                return strategy(visitor.Result.Value, request.GetToken());
            }

            public ValueTask OnStartAsync(IContext context)
            {
                return ValueTask.CompletedTask;
            }
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public static class BytesBuffer
    {
        public static IAddressPolicy Policy(Func<ReadOnlyMemory<byte>, bool> strategy)
        {
            return new InternalStrategyAddressPolicy(strategy);
        }

        public static IAddressPolicy Policy(ReadOnlyMemory<byte> bytes)
        {
            return new InternalStrategyAddressPolicy(value => value.Span.SequenceEqual(bytes.Span));
        }

        public static IAddress Address(ReadOnlyMemory<byte> bytes)
        {
            return new InternalBytesBuffer(bytes);
        }

        public static IPayload Payload(ReadOnlyMemory<byte> bytes)
        {
            return new InternalBytesBuffer(bytes);
        }

        public static IStreamHandler Process(Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> strategy)
        {
            return new InternalStrategyHandler(strategy);
        }

        public static IStreamHandler Process(Action<ReadOnlyMemory<byte>> strategy)
        {
            return new InternalStrategyHandler((buffer, _) =>
            {
                strategy(buffer);

                return ValueTask.CompletedTask;
            });
        }

        private class InternalBytesBuffer : IAddress, IPayload, IEquatable<IAddress>
        {
            private readonly ReadOnlyMemory<byte> value;

            public InternalBytesBuffer(ReadOnlyMemory<byte> value)
            {
                this.value = value;
            }

            IAddress ICloneable<IAddress>.Clone()
            {
                return this;
            }

            IPayload ICloneable<IPayload>.Clone()
            {
                return this;
            }

            public void Accept(IVisitor visitor)
            {
                visitor.Visit(value);
            }

            public IEquatable<IAddress> GetEquatable()
            {
                return this;
            }

            public bool Equals(IAddress other)
            {
                var visitor = new ValueVisitor();

                other.Accept(visitor);

                if (!visitor.Result.Success)
                {
                    return false;
                }

                return value.Span.SequenceEqual(visitor.Result.Value.Span);
            }

            public override int GetHashCode()
            {
                var result = new HashCode();
                
                result.AddBytes(value.Span);

                return result.ToHashCode();
            }

            public override string ToString()
            {
                return BitConverter.ToString(value.ToArray());
            }
        }

        private class ValueVisitor : IVisitor
        {
            public void Visit<T>(T value)
            {
                switch (value)
                {
                    case byte[] array:
                        Result = TryResult<ReadOnlyMemory<byte>>.True(array);
                        break;
                    case ReadOnlyMemory<byte> readOnlyMemory:
                        Result = TryResult<ReadOnlyMemory<byte>>.True(readOnlyMemory);
                        break;
                    case Memory<byte> memory:
                        Result = TryResult<ReadOnlyMemory<byte>>.True(memory);
                        break;
                }
            }

            public TryResult<ReadOnlyMemory<byte>> Result { get; private set; }
        }

        private class InternalStrategyAddressPolicy : IAddressPolicy
        {
            private readonly Func<ReadOnlyMemory<byte>, bool> strategy;

            public InternalStrategyAddressPolicy(Func<ReadOnlyMemory<byte>, bool> strategy)
            {
                this.strategy = strategy;
            }

            public bool IsMatch(IAddress address)
            {
                var visitor = new ValueVisitor();

                address.Accept(visitor);

                if (!visitor.Result.Success)
                {
                    return false;
                }

                return strategy(visitor.Result.Value);
            }
        }

        private class InternalStrategyHandler : IStreamHandler
        {
            private readonly Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> strategy;

            public InternalStrategyHandler(Func<ReadOnlyMemory<byte>, CancellationToken, ValueTask> strategy)
            {
                this.strategy = strategy;
            }

            public ValueTask OnReceiveAsync(IContext context)
            {
                var request = context.Get<IRequest>();
                var visitor = new ValueVisitor();

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

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation
{
    public static class StringBuffer
    {
        public static IAddressPolicy Policy(Func<string, bool> strategy)
        {
            return new InternalStringAddressPolicy(strategy);
        }

        public static IAddressPolicy Policy(string value)
        {
            return new InternalStringAddressPolicy(parsed => parsed == value);
        }

        public static IAddress Address(string value)
        {
            return new InternalStringBuffer(value);
        }

        public static IPayload Payload(string value)
        {
            return new InternalStringBuffer(value);
        }

        public static IStreamHandler Process(Func<string, CancellationToken, ValueTask> strategy)
        {
            return new InternalStrategyHandler(strategy);
        }

        public static IStreamHandler Process(Action<string> strategy)
        {
            return new InternalStrategyHandler((buffer, _) =>
            {
                strategy(buffer);

                return ValueTask.CompletedTask;
            });
        }

        private class InternalStringBuffer : IAddress, IPayload, IEquatable<IAddress>
        {
            private readonly string value;

            public InternalStringBuffer(string value)
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
                var visitor = new StringValueVisitor();

                other.Accept(visitor);

                if (visitor.Result == null)
                {
                    return false;
                }

                return visitor.Result == value;
            }

            public override int GetHashCode()
            {
                return value.GetHashCode();
            }

            public override string ToString()
            {
                return value;
            }
        }

        private class InternalStringAddressPolicy : IAddressPolicy
        {
            private readonly Func<string, bool> strategy;

            public InternalStringAddressPolicy(Func<string, bool> strategy)
            {
                this.strategy = strategy;
            }

            public bool IsMatch(IAddress address)
            {
                var visitor = new StringValueVisitor();

                address.Accept(visitor);

                if (visitor.Result == null)
                {
                    return false;
                }

                return strategy(visitor.Result);
            }
        }

        private class StringValueVisitor : IVisitor
        {
            public void Visit<T>(T value)
            {
                Result = value as string;
            }

            public string Result { get; private set; }
        }

        private class InternalStrategyHandler : IStreamHandler
        {
            private readonly Func<string, CancellationToken, ValueTask> strategy;

            public InternalStrategyHandler(Func<string, CancellationToken, ValueTask> strategy)
            {
                this.strategy = strategy;
            }

            public ValueTask OnReceiveAsync(IContext context)
            {
                var request = context.Get<IRequest>();
                var visitor = new StringValueVisitor();

                request.Accept(visitor);

                if (visitor.Result == null)
                {
                    return ValueTask.CompletedTask;
                }

                return strategy(visitor.Result, request.GetToken());
            }

            public ValueTask OnStartAsync(IContext context)
            {
                return ValueTask.CompletedTask;
            }
        }
    }
}

using System;

namespace Attractor.Implementation
{
    internal sealed class StringBuffer : IAddress, IPayload
    {
        private readonly string value;

        public StringBuffer(string value)
        {
            this.value = value;
        }

        public static IAddressPolicy CreatePolicy(Predicate<string> predicate)
        {
            return new AddressPolicy(predicate);
        }

        void IVisitable.Accept<T>(T visitor)
        {
            visitor.Visit(value);
        }

        public bool Equals(IAddress other)
        {
            var visitor = new ValueVisitor();

            other.Accept(visitor);

            if (!visitor.Result.Success)
            {
                return false;
            }

            return visitor.Result.Value == value;
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public override string ToString()
        {
            return value;
        }

        private class AddressPolicy : IAddressPolicy
        {
            private readonly Predicate<string> predicate;

            public AddressPolicy(Predicate<string> predicate)
            {
                this.predicate = predicate;
            }

            bool IAddressPolicy.IsMatch(IAddress address)
            {
                var visitor = new ValueVisitor();

                address.Accept(visitor);

                if (!visitor.Result.Success)
                {
                    return false;
                }

                return predicate(visitor.Result.Value);
            }
        }

        private struct ValueVisitor : IVisitor
        {
            public void Visit<T>(T value)
            {
                if (value is string str)
                {
                    Result = TryResult<string>.True(str);
                }
            }

            public TryResult<string> Result { get; private set; }
        }
    }
}

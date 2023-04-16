using System;

namespace Attractor.Implementation
{
    internal sealed class BytesBuffer : IAddress, IPayload
    {
        private readonly ReadOnlyMemory<byte> value;

        public BytesBuffer(ReadOnlyMemory<byte> value)
        {
            this.value = value;
        }

        public static IAddressPolicy CreatePolicy(Predicate<ReadOnlyMemory<byte>> predicate)
        {
            return new AddressPolicy(predicate);
        }

        void IVisitable.Accept<T>(T visitor)
        {
            visitor.Visit(value);
        }

        bool IEquatable<IAddress>.Equals(IAddress other)
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

            //unchecked
            //{
            //    var result = 0;

            //    foreach (var b in bytes.AsSpan())
            //    {
            //        result = (result * 31) ^ b;
            //    }

            //    return result;
            //}

            //var result = new HashCode();

            //var longs = MemoryMarshal.Cast<byte, long>(bytes.AsSpan());
            //var rest = bytes.AsSpan().Slice(longs.Length * sizeof(long));

            //result.AddBytes(rest);

            //foreach (long l in longs)
            //{
            //    result.Add(l);
            //}

            //foreach (byte b in rest)
            //{
            //    result.Add(b);
            //}

            //result.AddBytes(bytes.Value.Span);

            //return result.ToHashCode();
        }

        public override string ToString()
        {
            return BitConverter.ToString(value.ToArray());
        }

        private class AddressPolicy : IAddressPolicy
        {
            private readonly Predicate<ReadOnlyMemory<byte>> predicate;

            public AddressPolicy(Predicate<ReadOnlyMemory<byte>> predicate)
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
    }
}

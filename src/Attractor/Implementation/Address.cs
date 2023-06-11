using System;
using System.Threading;

namespace Attractor.Implementation
{
    public static class Address
    {
        private static readonly ThreadLocal<ValueVisitor> visitorFactory = new(() => new ValueVisitor());
        
        public static IAddressPolicy FromString(Predicate<string> predicate)
        {
            return new StringAddressPolicy(predicate);
        }
        
        public static IAddress FromString(string value)
        {
            return new StringAddress(value);
        }

        public static IAddressPolicy FromBytes(Predicate<byte[]> predicate)
        {
            return new BytesAddressPolicy(predicate);
        }

        public static IAddress FromBytes(params byte[] value)
        {
            return new BytesAddress(value);
        }

        private class BytesAddress : IAddress
        {
            private readonly byte[] value;

            public BytesAddress(byte[] value)
            {
                this.value = value;
            }
            
            void IVisitable.Accept<T>(T visitor)
            {
                visitor.Visit(value);
            }

            public bool Equals(IAddress other)
            {
                if (other == null)
                {
                    return false;
                }
                
                using var visitor = visitorFactory.Value;

                other.Accept(visitor);

                if (!visitor.IsBytes)
                {
                    return false;
                }

                return value.AsSpan().SequenceEqual(visitor.Bytes);
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as IAddress);
            }

            public override int GetHashCode()
            {
                var result = new HashCode();
                
                result.AddBytes(value);

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
                return BitConverter.ToString(value);
            }
        }

        private class BytesAddressPolicy : IAddressPolicy
        {
            private readonly Predicate<byte[]> predicate;

            public BytesAddressPolicy(Predicate<byte[]> predicate)
            {
                this.predicate = predicate;
            }

            bool IAddressPolicy.IsMatch(IAddress address)
            {
                using var visitor = visitorFactory.Value;
                
                address.Accept(visitor);

                if (!visitor.IsBytes)
                {
                    return false;
                }

                return predicate(visitor.Bytes);
            }
        }

        private class StringAddress : IAddress
        {
            private readonly string value;

            public StringAddress(string value)
            {
                this.value = value;
            }
            
            void IVisitable.Accept<T>(T visitor)
            {
                visitor.Visit(value);
            }

            public bool Equals(IAddress other)
            {
                if (other == null)
                {
                    return false;
                }
                
                using var visitor = visitorFactory.Value;

                other.Accept(visitor);

                if (!visitor.IsString)
                {
                    return false;
                }

                return visitor.String == value;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as IAddress);
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

        private class StringAddressPolicy : IAddressPolicy
        {
            private readonly Predicate<string> predicate;

            public StringAddressPolicy(Predicate<string> predicate)
            {
                this.predicate = predicate;
            }

            bool IAddressPolicy.IsMatch(IAddress address)
            {
                using var visitor = visitorFactory.Value;
                
                address.Accept(visitor);

                if (!visitor.IsString)
                {
                    return false;
                }

                return predicate(visitor.String);
            }
        }

        private class ValueVisitor : IVisitor, IDisposable
        {
            void IVisitor.Visit<T>(T value)
            {
                switch (value)
                {
                    case byte[] arr:
                        IsBytes = true;
                        Bytes = arr;
                        break;
                    case string str:
                        IsString = true;
                        String = str;
                        break;
                }
            }

            void IDisposable.Dispose()
            {
                IsBytes = false;
                IsString = false;
                Bytes = null;
                String = null;
            }

            public bool IsBytes { get; private set; }

            public byte[] Bytes { get; private set; }

            public bool IsString { get; private set; }

            public string String { get; private set; }
        }
    }
}
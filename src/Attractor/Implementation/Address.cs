using System;

namespace Attractor.Implementation
{
    public static class Address
    {
        public static IAddressPolicy FromString(Predicate<string> predicate)
        {
            return StringBuffer.CreatePolicy(predicate);
        }
        
        public static IAddress FromString(string value)
        {
            return new StringBuffer(value);
        }

        public static IAddressPolicy FromBytes(Predicate<ReadOnlyMemory<byte>> predicate)
        {
            return BytesBuffer.CreatePolicy(predicate);
        }

        public static IAddress FromBytes(ReadOnlyMemory<byte> value)
        {
            return new BytesBuffer(value);
        }
    }
}
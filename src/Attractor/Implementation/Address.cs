using System;
using System.Collections.Generic;

namespace Attractor.Implementation;

public static class Address
{
    public static IEqualityComparer<IAddress> EqualityComparer { get; } = new AddressEqualityComparer();

    public static IAddress Empty { get; } = new BytesAddress([]);

    public static IAddressPolicy FromStrategy(Predicate<IAddress> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate, nameof(predicate));

        return new StrategyAddressPolicy(predicate);
    }

    public static IAddressPolicy FromExact(IAddress address)
    {
        ArgumentNullException.ThrowIfNull(address, nameof(address));

        return FromStrategy(address.Equals);
    }

    public static IAddress FromBytes(params byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        return new BytesAddress(value);
    }

    private class BytesAddress(byte[] bytes) : IAddress
    {
        public bool Equals(IAddress other)
        {
            return other != null && other.GetBytes().SequenceEqual(bytes);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as IAddress);
        }

        public ReadOnlySpan<byte> GetBytes()
        {
            return bytes;
        }

        public override int GetHashCode()
        {
            var result = new HashCode();

            result.AddBytes(bytes);

            return result.ToHashCode();
        }

        public override string ToString()
        {
            return BitConverter.ToString(bytes);
        }
    }

    private class AddressEqualityComparer : IEqualityComparer<IAddress>
    {
        public bool Equals(IAddress first, IAddress second)
        {
            ArgumentNullException.ThrowIfNull(first, nameof(first));
            ArgumentNullException.ThrowIfNull(second, nameof(second));

            return first.Equals(second);
        }

        public int GetHashCode(IAddress obj)
        {
            ArgumentNullException.ThrowIfNull(obj, nameof(obj));

            return obj.GetHashCode();
        }
    }

    private class StrategyAddressPolicy(Predicate<IAddress> Strategy) : IAddressPolicy
    {
        bool IAddressPolicy.IsMatch(IAddress address)
        {
            return Strategy(address);
        }
    }
}

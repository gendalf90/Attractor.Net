using System;
using System.Collections.Generic;
using System.Threading;

namespace Attractor.Implementation;

public static class Address
{
    private static readonly AsyncLocal<IAddress> CurrentAddress = new();

    public static IEqualityComparer<IAddress> EqualityComparer { get; } = new AddressEqualityComparer();

    public static IAddress Current => CurrentAddress.Value;

    public static IRouter FromStrategy(Predicate<IAddress> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate, nameof(predicate));

        return new StrategyAddressPolicy(predicate);
    }

    public static IRouter FromExact(IAddress address)
    {
        ArgumentNullException.ThrowIfNull(address, nameof(address));

        return FromStrategy(value => EqualityComparer.Equals(value, address));
    }

    public static IAddress FromString(string value)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        return new StringAddress(value);
    }

    internal static IDisposable UseAddress(IAddress address)
    {
        var current = CurrentAddress.Value;

        CurrentAddress.Value = address;

        return Disposable.Create(() => CurrentAddress.Value = current);
    }
    
    private class StringAddress(string str) : IAddress
    {
        public string Value => str;

        public override bool Equals(object obj)
        {
            if (obj is not IAddress other)
            {
                return false;
            }

            return EqualityComparer.Equals(this, other);
        }

        public override int GetHashCode()
        {
            return EqualityComparer.GetHashCode(this);
        }
    }

    private class AddressEqualityComparer : IEqualityComparer<IAddress>
    {
        public bool Equals(IAddress first, IAddress second)
        {
            return ReferenceEquals(first, second) || string.Equals(first.Value, second.Value, StringComparison.Ordinal);
        }

        public int GetHashCode(IAddress obj)
        {
            return obj == null ? 0 : obj.Value.GetHashCode(StringComparison.Ordinal);
        }
    }

    private class StrategyAddressPolicy(Predicate<IAddress> Strategy) : IRouter
    {
        bool IRouter.IsMatch(IAddress address)
        {
            return Strategy(address);
        }
    }
}

using System;
using System.Collections.Generic;

namespace Attractor.Implementation
{
    public sealed class AddressEqualityComparer : IEqualityComparer<IAddress>
    {
        public static AddressEqualityComparer Default { get; } = new AddressEqualityComparer();

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
}

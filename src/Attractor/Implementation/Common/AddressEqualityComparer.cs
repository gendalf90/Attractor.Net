using System;
using System.Collections.Generic;
using System.Linq;

namespace Attractor.Implementation.Common
{
    internal sealed class AddressEqualityComparer : IEqualityComparer<IAddress>
    {
        public bool Equals(IAddress first, IAddress second)
        {
            var firstBytes = first.GetBytes();
            var secondBytes = second.GetBytes();

            return firstBytes.Span.SequenceEqual(secondBytes.Span);
        }

        public int GetHashCode(IAddress obj)
        {
            unchecked
            {
                var bytes = obj.GetBytes();
                var result = 0;

                foreach (var b in bytes.Span)
                {
                    result = result * 31 + b;
                }

                return result;
            }
        }
    }
}

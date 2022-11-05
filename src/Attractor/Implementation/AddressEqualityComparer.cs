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

            return first.GetEquatable().Equals(second);
        }

        public int GetHashCode(IAddress obj)
        {
            ArgumentNullException.ThrowIfNull(obj, nameof(obj));

            return obj.GetEquatable().GetHashCode();

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
    }
}

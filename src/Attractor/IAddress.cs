using System;

namespace Attractor
{
    public interface IAddress : IEquatable<IAddress>
    {
        ReadOnlySpan<byte> AsSpan();
    }
}

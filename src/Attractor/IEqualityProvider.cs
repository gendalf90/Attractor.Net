using System;

namespace Attractor
{
    public interface IEqualityProvider<T>
    {
        IEquatable<T> GetEquatable();
    }
}

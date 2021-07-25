using System;

namespace Attractor
{
    public interface IAddress
    {
        ReadOnlyMemory<byte> GetBytes();
    }
}

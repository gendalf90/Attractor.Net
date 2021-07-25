using System;

namespace Attractor
{
    public interface IPayload
    {
        ReadOnlyMemory<byte> GetBytes();
    }
}

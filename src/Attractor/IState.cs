using System;

namespace Attractor
{
    public interface IState
    {
        ReadOnlyMemory<byte> GetBytes();
    }
}

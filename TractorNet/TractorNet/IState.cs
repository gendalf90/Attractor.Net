using System;

namespace TractorNet
{
    public interface IState
    {
        ReadOnlyMemory<byte> GetBytes();
    }
}

using System;

namespace TractorNet
{
    public interface IAddress
    {
        ReadOnlyMemory<byte> GetBytes();
    }
}

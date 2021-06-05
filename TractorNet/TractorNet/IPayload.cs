using System;

namespace TractorNet
{
    public interface IPayload
    {
        ReadOnlyMemory<byte> GetBytes();
    }
}

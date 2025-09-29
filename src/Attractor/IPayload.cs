using System;

namespace Attractor;

public interface IPayload
{
    ReadOnlySpan<byte> Bytes { get; }
}

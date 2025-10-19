using System;

namespace Attractor.Implementation;

public static class Payload
{
    public static IPayload Empty { get; } = new BytesPayload([]);

    public static IPayload FromBytes(params byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value, nameof(value));

        return new BytesPayload(value);
    }

    private class BytesPayload(byte[] value) : IPayload
    {
        public ReadOnlySpan<byte> GetBytes()
        {
            return value;
        }

        public override string ToString()
        {
            return BitConverter.ToString(value);
        }
    }
}

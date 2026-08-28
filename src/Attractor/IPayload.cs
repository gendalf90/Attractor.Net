using System.Buffers;

namespace Attractor;

public interface IPayload
{
    void Write(IBufferWriter<byte> buffer);
}

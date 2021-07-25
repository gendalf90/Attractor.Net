using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IOutbox
    {
        ValueTask SendMessageAsync(IAddress address, IPayload payload, SendingMetadata metadata = default, CancellationToken token = default);
    }
}

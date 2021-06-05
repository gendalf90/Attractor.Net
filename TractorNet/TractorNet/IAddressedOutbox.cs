using System.Threading;
using System.Threading.Tasks;

namespace TractorNet
{
    public interface IAddressedOutbox
    {
        ValueTask SendMessageAsync(IPayload payload, SendingMetadata metadata = default, CancellationToken token = default);
    }
}

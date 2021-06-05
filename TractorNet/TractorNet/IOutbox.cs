using System.Threading;
using System.Threading.Tasks;

namespace TractorNet
{
    public interface IOutbox
    {
        ValueTask SendMessageAsync(IAddress address, IPayload payload, SendingMetadata metadata = default, CancellationToken token = default);
    }
}

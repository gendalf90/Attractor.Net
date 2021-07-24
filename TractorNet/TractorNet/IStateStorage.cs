using System.Threading;
using System.Threading.Tasks;

namespace TractorNet
{
    public interface IStateStorage
    {
        ValueTask LoadStateAsync(IProcessingMessage message, CancellationToken token = default);
    }
}

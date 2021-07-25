using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IStateStorage
    {
        ValueTask LoadStateAsync(IProcessingMessage message, CancellationToken token = default);
    }
}

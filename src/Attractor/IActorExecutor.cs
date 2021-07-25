using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IActorExecutor
    {
        ValueTask<bool> TryExecuteAsync(IProcessingMessage message, CancellationToken token = default);
    }
}

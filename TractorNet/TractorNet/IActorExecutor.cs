using System.Threading;
using System.Threading.Tasks;

namespace TractorNet
{
    public interface IActorExecutor
    {
        ValueTask ExecuteAsync(IProcessingMessage message, CancellationToken token = default);
    }
}

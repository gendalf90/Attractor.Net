using System.Threading;
using System.Threading.Tasks;

namespace TractorNet
{
    public interface IActorExecutorFactory
    {
        ValueTask<TryResult<IActorExecutor>> TryCreateByAddressAsync(IAddress address, CancellationToken token = default);
    }
}

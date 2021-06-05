using System.Threading;
using System.Threading.Tasks;

namespace TractorNet
{
    public interface IActorExecutorCreator
    {
        Task<TryResult<IActorExecutor>> TryCreateByAddressAsync(IAddress address, CancellationToken token = default);
    }
}

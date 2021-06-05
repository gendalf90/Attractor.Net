using System.Threading;
using System.Threading.Tasks;

namespace TractorNet
{
    public interface IAddressPolicy
    {
        ValueTask<bool> IsMatchAsync(IAddress address, CancellationToken token = default);
    }
}

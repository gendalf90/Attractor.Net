using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IAddressPolicy
    {
        ValueTask<bool> IsMatchAsync(IAddress address, CancellationToken token = default);
    }
}

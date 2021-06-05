using System.Threading;
using System.Threading.Tasks;

namespace TractorNet.Implementation.Address
{
    internal sealed class MatchAllAddressesPolicy : IAddressPolicy
    {
        public ValueTask<bool> IsMatchAsync(IAddress address, CancellationToken token = default)
        {
            return ValueTask.FromResult(true);
        }
    }
}

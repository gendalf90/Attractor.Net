using Attractor.Implementation.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor.Implementation.Address
{
    internal sealed class RejectAllAddressesPolicy : IAddressPolicy
    {
        public ValueTask<bool> IsMatchAsync(IAddress address, CancellationToken token = default)
        {
            return ValueTaskBuilder.FromResult(false);
        }
    }
}

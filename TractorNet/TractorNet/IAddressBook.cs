using System;
using System.Threading;
using System.Threading.Tasks;

namespace TractorNet
{
    public interface IAddressBook
    {
        ValueTask<TryResult<IAsyncDisposable>> TryUseAddressAsync(IAddress address, CancellationToken token = default);
    }
}

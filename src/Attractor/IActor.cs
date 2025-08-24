using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IActor : IAsyncDisposable
    {
        //OfferStatus OfferMessage(IContext context, CancellationToken token = default);

        Task OnReceiveAsync(IContext context, CancellationToken token = default);
    }
}

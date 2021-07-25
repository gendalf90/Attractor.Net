using System;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IReceivedMessageFeature : IAddress, IPayload
    {
        ValueTask ConsumeAsync(CancellationToken token = default);

        ValueTask DelayAsync(TimeSpan time, CancellationToken token = default);

        ValueTask ExpireAsync(TimeSpan time, CancellationToken token = default);
    }
}

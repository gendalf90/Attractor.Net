using System;
using System.Threading;
using System.Threading.Tasks;

namespace TractorNet
{
    public interface IReceivedMessage : IMessage
    {
        ValueTask ConsumeAsync(CancellationToken token = default);

        ValueTask DelayAsync(TimeSpan time, CancellationToken token = default);

        ValueTask ExpireAsync(TimeSpan time, CancellationToken token = default);
    }
}

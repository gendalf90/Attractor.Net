using System.Collections.Generic;
using System.Threading;

namespace TractorNet
{
    public interface IInbox
    {
        IAsyncEnumerable<IProcessingMessage> ReadMessagesAsync(CancellationToken token = default);
    }
}

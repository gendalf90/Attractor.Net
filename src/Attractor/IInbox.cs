using System.Collections.Generic;
using System.Threading;

namespace Attractor
{
    public interface IInbox
    {
        IAsyncEnumerable<IProcessingMessage> ReadMessagesAsync(CancellationToken token = default);
    }
}

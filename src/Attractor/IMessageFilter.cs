using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IMessageFilter
    {
        ValueTask<bool> IsMatchAsync(IList context, CancellationToken token = default);
    }
}
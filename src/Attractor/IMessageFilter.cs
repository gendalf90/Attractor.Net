using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IMessageFilter
    {
        ValueTask<bool> IsMatchAsync(IContext context, CancellationToken token = default);
    }
}
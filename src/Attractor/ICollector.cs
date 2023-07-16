using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface ICollector
    {
        ValueTask OnCollectAsync(IContext context, CancellationToken token = default);
    }
}

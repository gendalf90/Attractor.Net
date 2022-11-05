using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface ISystem
    {
        ValueTask<IRef> GetAsync(IAddress address, CancellationToken token = default);
    }
}

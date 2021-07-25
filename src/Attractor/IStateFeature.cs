using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IStateFeature : IState
    {
        ValueTask SaveAsync(IState state, CancellationToken token = default);
    }
}

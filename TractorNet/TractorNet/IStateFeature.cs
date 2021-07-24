using System.Threading;
using System.Threading.Tasks;

namespace TractorNet
{
    public interface IStateFeature : IState
    {
        ValueTask SaveAsync(IState state, CancellationToken token = default);
    }
}

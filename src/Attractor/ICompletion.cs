using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface ICompletion
    {
        Task WaitAsync(CancellationToken token = default);
    }
}

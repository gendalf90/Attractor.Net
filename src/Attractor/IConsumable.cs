using System.Threading;
using System.Threading.Tasks;

namespace Attractor
{
    public interface IConsumable
    {
        ValueTask ConsumeAsync(CancellationToken token = default);
    }
}
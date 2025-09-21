using System.Threading;
using System.Threading.Tasks;

namespace Attractor;

public interface IScheduler
{
    Task<Try<IHandle>> TryLockAsync(IAddress address, CancellationToken token = default);
}
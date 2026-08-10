using System.Threading;
using System.Threading.Tasks;

namespace Attractor;

public interface IRef
{
    Task Send(IMessage message, CancellationToken token = default);
}

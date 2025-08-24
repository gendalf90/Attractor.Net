using System.Threading;
using System.Threading.Tasks;

namespace Attractor;

public interface IMessage
{
    Task PostponeAsync(CancellationToken token = default);
    
    Task AcceptAsync(CancellationToken token = default);
}
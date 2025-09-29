using System.Threading;

namespace Attractor;

public interface IRequestCancellation
{
    CancellationToken Token { get; }
}
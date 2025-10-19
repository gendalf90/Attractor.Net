using System.Threading;

namespace Attractor;

public interface ICancellation
{
    CancellationToken Cancellation { get; }
}